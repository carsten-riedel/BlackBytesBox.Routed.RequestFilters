using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Utility.HttpContextUtility;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on the URI segments of the request URL
    /// against configured whitelist and blacklist patterns.
    /// </summary>
    public class CIDRFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<CIDRFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<CIDRFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        // A static MemoryCache instance is used to cache results.
        // Optionally, you can set a SizeLimit in the MemoryCacheOptions to limit to roughly 1000 entries.
        private static readonly MemoryCache _cidrCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 10000 // Uncomment and set sizes on each entry if needed.
        });

        // Private property to store the filter priority.
        private bool _whitelistFirst;

        /// <summary>
        /// Initializes a new instance of the <see cref="CIDRFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance for recording middleware operations.</param>
        /// <param name="optionsMonitor">The monitor for retrieving the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points when filtering fails.</param>
        public CIDRFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<CIDRFilteringMiddleware> logger, IOptionsMonitor<CIDRFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            // Pre-calculate filter priority based on current options.
            _whitelistFirst = _optionsMonitor.CurrentValue.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(CIDRFilteringMiddleware));
                _whitelistFirst = updatedOptions.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating the URI segments of the request URL
        /// against the configured whitelist and blacklist patterns.
        /// If any segment is blacklisted or the full URI cannot be built, the request is treated as not allowed.
        /// If at least one segment is whitelisted, the request is forwarded.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Bypass filtering for localhost requests.
            if (context.Connection.RemoteIpAddress != null &&
                (IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
                 context.Connection.RemoteIpAddress.Equals(IPAddress.IPv6Loopback)))
            {
                _logger.LogDebug("Bypassing CIDR filtering for localhost.");
                await _nextMiddleware(context);
                return;
            }

            var options = _optionsMonitor.CurrentValue;
            var fullUri = HttpContextUtility.GetUriFromRequestDisplayUrl(context, _logger);
            var displayUrl = context.Request.GetDisplayUrl();

            bool IsInWhiteList = false;
            bool IsInBlackList = false;
            // Use pre-calculated filter priority to decide branch.
            if (_whitelistFirst)
            {
                IsInWhiteList = IsInList(context.Connection.RemoteIpAddress, options.Whitelist);
                if (IsInWhiteList)
                {
                    await WhitelistedAsync(context, options);
                    return;
                }
                IsInBlackList = IsInList(context.Connection.RemoteIpAddress, options.Blacklist);
                if (IsInBlackList)
                {
                    await BlacklistedAsync(context, options);
                    return;
                }
            }
            else
            {
                IsInBlackList = IsInList(context.Connection.RemoteIpAddress, options.Blacklist);
                if (IsInBlackList)
                {
                    await BlacklistedAsync(context, options);
                    return;
                }
                IsInWhiteList = IsInList(context.Connection.RemoteIpAddress, options.Whitelist);
                if (IsInWhiteList)
                {
                    await WhitelistedAsync(context, options);
                    return;
                }
            }

            bool NotMatched = (!IsInWhiteList && !IsInBlackList);
            // Check if the host matches any of the configured patterns.
            if (NotMatched)
            {
                await NotMatchedAsync(context, options);
                return;
            }
        }

        /// <summary>
        /// Determines whether the specified IP address is contained in any of the provided CIDR ranges.
        /// The result is cached for 5 minutes based on a composite key built from the IP address and CIDR list.
        /// </summary>
        /// <param name="ipAddress">
        /// The <see cref="IPAddress"/> to test; if null, the method returns false.
        /// </param>
        /// <param name="cidrlist">
        /// An array of CIDR notation strings representing the IP ranges (e.g., "192.168.1.0/24", "2001:db8::/64").
        /// </param>
        /// <returns>
        /// A <see cref="Task{Boolean}"/> that returns true if <paramref name="ipAddress"/> is within any of the ranges;
        /// otherwise, false.
        /// </returns>
        private bool IsInList(IPAddress? ipAddress, string[]? cidrlist)
        {
            if (ipAddress == null || cidrlist == null || cidrlist.Length == 0)
            {
                return false;
            }

            // If any CIDR string contains a '*' wildcard, immediately allow the IP (skip calculations).
            if (cidrlist.Any(cidr => cidr.Equals("*")))
            {
                return true;
            }

            // Build a composite cache key from the IP address and the sorted CIDR list.
            string cacheKey = BuildIsInListCacheKey(ipAddress, cidrlist);
            if (_cidrCache.TryGetValue(cacheKey, out bool cachedOverallResult))
            {
                return cachedOverallResult;
            }

            bool result = false;
            foreach (var cidr in cidrlist)
            {
                // For each CIDR string, retrieve (or compute) its cached range.
                var rangeResult = GetCachedCIDRRangeResult(cidr);
                // Check if the input IP is within the calculated range.
                if (CIDRCalculator.IsInRange(ipAddress, rangeResult))
                {
                    result = true;
                    break;
                }
            }

            // Cache the overall IsInList result for 5 minutes.
            var overallCacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }.SetSize(1);
            _cidrCache.Set(cacheKey, result, overallCacheOptions);

            return result;
        }

        /// <summary>
        /// Constructs a composite cache key for the overall IsInList method, based on the IP address and the CIDR list.
        /// The CIDR list is sorted to ensure the key is consistent regardless of the provided order.
        /// </summary>
        /// <param name="ipAddress">The IP address being checked.</param>
        /// <param name="cidrlist">The array of CIDR notation strings.</param>
        /// <returns>A string key representing the combination.</returns>
        private string BuildIsInListCacheKey(IPAddress ipAddress, string[] cidrlist)
        {
            var sortedCidrs = cidrlist.OrderBy(c => c).ToArray();
            string joinedCidrs = string.Join(",", sortedCidrs);
            return $"IsInList:{ipAddress}|{joinedCidrs}";
        }

        /// <summary>
        /// Retrieves the CIDR range calculation for a given CIDR string from the cache.
        /// If not already cached, the calculation is performed, cached for 5 minutes, and then returned.
        /// </summary>
        /// <param name="cidr">A CIDR notation string (e.g., "192.168.1.0/24" or "2001:db8::/64").</param>
        /// <returns>
        /// A <see cref="CIDRRangeResultClass"/> containing the lower and upper bounds of the IP range.
        /// </returns>
        private CIDRRangeResultClass GetCachedCIDRRangeResult(string cidr)
        {
            if (_cidrCache.TryGetValue(cidr, out CIDRRangeResultClass cachedRange))
            {
                return cachedRange;
            }

            var result = CIDRCalculator.CalculateIPRange(cidr);
            var cidrCacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            }.SetSize(1);
            // If you are using a size limit, you can set the entry size, e.g.:
            // cidrCacheOptions.SetSize(1);
            _cidrCache.Set(cidr, result, cidrCacheOptions);
            return result;
        }

        /// <summary>
        /// Asynchronously handles requests for which the URL (displayUrl) does not match any whitelist or blacklist patterns.
        /// </summary>
        /// <remarks>
        /// Depending on the options provided, this method logs the event and either continues processing the request
        /// or aborts it by writing a default status code response. The <paramref name="displayUrl"/> parameter is used to log
        /// the URL segments that were derived from the request path.
        /// </remarks>
        /// <param name="context">The current HTTP context of the request.</param>
        /// <param name="options">The configuration options for segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>
        /// <code>
        /// // Example usage:
        /// await NotMatchedAsync(httpContext, middlewareOptions, "/segment1/segment2/segment3");
        /// </code>
        /// </example>
        public async Task NotMatchedAsync(HttpContext context, CIDRFilteringMiddlewareOptions options)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(CIDRFilteringMiddleware),
                options.NotMatchedFailureRating,
                DateTime.UtcNow);

            if (options.NotMatchedContinue)
            {
                _logger.Log(
                    options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,
                    "NotMatched continue: CIDR filter '{IP}' matched a entry.",
                    context.GetItem<string>("remoteIpAddressStr"));
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.Log(
                    options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,
                    "NotMatched aborting: CIDR filter '{IP}' matched a entry.",
                    context.GetItem<string>("remoteIpAddressStr"));
                await context.Response.WriteDefaultStatusCodeAnswer(options.NotMatchedStatusCode);
                return;
            }
        }

        /// <summary>
        /// Asynchronously handles requests that match a blacklisted URL pattern.
        /// </summary>
        /// <remarks>
        /// Depending on the provided options, this method logs the event and either continues processing the request
        /// or aborts it by writing a default status code response. The <paramref name="displayUrl"/> parameter is used to log
        /// the URL segments that triggered the blacklist rule.
        /// </remarks>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="options">The configuration options for the segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <param name="firstBlacklistedSegment">The first matching blacklisted pattern result.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task BlacklistedAsync(HttpContext context, CIDRFilteringMiddlewareOptions options)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(CIDRFilteringMiddleware),
                options.BlacklistFailureRating,
                DateTime.UtcNow);

            if (options.BlacklistContinue)
            {
                _logger.LogDebug("Blacklisted continue: CIDR filter '{IP}' matched a entry.", context.GetItem<string>("remoteIpAddressStr"));
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogDebug("Blacklisted aborting: CIDR filter '{IP}' matched a entry.", context.GetItem<string>("remoteIpAddressStr"));
                await context.Response.WriteDefaultStatusCodeAnswer(options.BlacklistStatusCode);
                return;
            }
        }

        /// <summary>
        /// Asynchronously handles requests that match a whitelisted URL pattern by continuing the middleware pipeline.
        /// </summary>
        /// <remarks>
        /// This method logs the event using the <paramref name="displayUrl"/> parameter and then continues processing the request
        /// by calling the next middleware.
        /// </remarks>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="options">The configuration options for the segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <param name="firstWhitelistedSegment">The first matching whitelisted pattern result.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WhitelistedAsync(HttpContext context, CIDRFilteringMiddlewareOptions options)
        {
            _logger.LogDebug("Whitelisted continue: CIDR filter '{IP}' matched a entry.", context.GetItem<string>("remoteIpAddressStr"));
            await _nextMiddleware(context);
            return;
        }

        /// <summary>
        /// Represents the result of a CIDR range calculation.
        /// </summary>
        public class CIDRRangeResultClass
        {
            /// <summary>
            /// Gets or sets the lower bound (network address) of the IP range.
            /// </summary>
            public IPAddress LowerBound { get; set; }

            /// <summary>
            /// Gets or sets the upper bound (broadcast or last address) of the IP range.
            /// </summary>
            public IPAddress UpperBound { get; set; }
        }

        /// <summary>
        /// Provides methods for performing CIDR range calculations.
        /// Supports both IPv4 and IPv6 addresses.
        /// </summary>
        public static class CIDRCalculator
        {
            /// <summary>
            /// Calculates the network (lower bound) and broadcast/last (upper bound) IP addresses for a given CIDR notation.
            /// </summary>
            /// <param name="cidr">
            /// The CIDR notation string (e.g., "192.168.1.0/24" or "2001:db8::/64").
            /// </param>
            /// <returns>
            /// A <see cref="CIDRRangeResultClass"/> instance containing the lower and upper bounds as <see cref="IPAddress"/>.
            /// </returns>
            /// <remarks>
            /// The method splits the CIDR notation, validates the IP address and prefix length, then converts the IP
            /// to a numeric value using <see cref="BigInteger"/>. Bitwise operations are performed to calculate the network
            /// and broadcast (or last) addresses, which are finally converted back to <see cref="IPAddress"/>.
            /// </remarks>
            /// <example>
            /// <code>
            /// // Example for IPv4:
            /// var result = CIDRCalculator.CalculateIPRange("192.168.1.0/24");
            /// Console.WriteLine($"Network Address: {result.LowerBound}");
            /// Console.WriteLine($"Broadcast Address: {result.UpperBound}");
            /// 
            /// // Example for IPv6:
            /// var result6 = CIDRCalculator.CalculateIPRange("2001:db8::/64");
            /// Console.WriteLine($"Network Address: {result6.LowerBound}");
            /// Console.WriteLine($"Last Address: {result6.UpperBound}");
            /// </code>
            /// </example>
            public static CIDRRangeResultClass CalculateIPRange(string cidr)
            {
                if (string.IsNullOrWhiteSpace(cidr))
                    throw new ArgumentException("CIDR notation cannot be null or empty.", nameof(cidr));

                // Split the CIDR notation into IP and prefix parts.
                string[] parts = cidr.Split('/');
                if (parts.Length != 2)
                    throw new FormatException("Invalid CIDR notation format.");

                // Parse the IP address.
                if (!IPAddress.TryParse(parts[0], out IPAddress ipAddress))
                    throw new FormatException("Invalid IP address.");

                // Parse the prefix length.
                if (!int.TryParse(parts[1], out int prefixLength))
                    throw new FormatException("Invalid prefix length.");

                int bitLength;
                // Determine the bit length based on the address family.
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    bitLength = 32;
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    bitLength = 128;
                }
                else
                {
                    throw new NotSupportedException("Only IPv4 and IPv6 addresses are supported.");
                }

                // Validate that the prefix length is within the allowed range.
                if (prefixLength < 0 || prefixLength > bitLength)
                    throw new ArgumentOutOfRangeException(nameof(prefixLength), $"Prefix length must be between 0 and {bitLength}.");

                // Convert the IP address to a BigInteger.
                byte[] ipBytes = ipAddress.GetAddressBytes();
                // BigInteger expects a little-endian byte array, so create a copy and reverse the order.
                byte[] ipBytesLE = (byte[])ipBytes.Clone();
                Array.Reverse(ipBytesLE);

                // Ensure the BigInteger treats the bytes as unsigned.
                if (ipBytesLE[ipBytesLE.Length - 1] >= 0x80)
                {
                    Array.Resize(ref ipBytesLE, ipBytesLE.Length + 1);
                    ipBytesLE[ipBytesLE.Length - 1] = 0;
                }
                BigInteger ipInt = new BigInteger(ipBytesLE);

                // Calculate the maximum value for the IP address (e.g., 2^32 - 1 for IPv4, 2^128 - 1 for IPv6).
                BigInteger maxValue = (BigInteger.One << bitLength) - 1;

                // Create a subnet mask with prefixLength bits set to 1.
                BigInteger mask = ~((BigInteger.One << (bitLength - prefixLength)) - 1) & maxValue;

                // Compute the network (lower bound) and the broadcast/last address (upper bound).
                BigInteger networkInt = ipInt & mask;
                BigInteger broadcastInt = networkInt | (~mask & maxValue);

                // Convert the numeric values back to IPAddress.
                IPAddress lowerBound = BigIntegerToIPAddress(networkInt, bitLength);
                IPAddress upperBound = BigIntegerToIPAddress(broadcastInt, bitLength);

                return new CIDRRangeResultClass
                {
                    LowerBound = lowerBound,
                    UpperBound = upperBound
                };
            }

            /// <summary>
            /// Determines if the given <see cref="IPAddress"/> is within the specified CIDR range.
            /// </summary>
            /// <param name="ip">
            /// The <see cref="IPAddress"/> to test; if null, the method returns false.
            /// </param>
            /// <param name="range">
            /// The <see cref="CIDRRangeResultClass"/> containing the lower and upper bounds of the range.
            /// </param>
            /// <returns>
            /// True if <paramref name="ip"/> is within the range defined by <paramref name="range"/>, otherwise false.
            /// </returns>
            public static bool IsInRange(IPAddress? ip, CIDRRangeResultClass range)
            {
                if (ip == null)
                    return false;

                if (range == null)
                    throw new ArgumentNullException(nameof(range));

                // Convert the IP addresses to BigInteger.
                BigInteger ipInt = ConvertIPAddressToBigInteger(ip);
                BigInteger lowerInt = ConvertIPAddressToBigInteger(range.LowerBound);
                BigInteger upperInt = ConvertIPAddressToBigInteger(range.UpperBound);

                // Return true if the IP value is within the inclusive range.
                return ipInt >= lowerInt && ipInt <= upperInt;
            }

            /// <summary>
            /// Converts an <see cref="IPAddress"/> to its unsigned <see cref="BigInteger"/> representation.
            /// </summary>
            /// <param name="ipAddress">The IP address to convert.</param>
            /// <returns>The <see cref="BigInteger"/> representation of the IP address.</returns>
            private static BigInteger ConvertIPAddressToBigInteger(IPAddress ipAddress)
            {
                byte[] ipBytes = ipAddress.GetAddressBytes();
                // BigInteger expects the byte array in little-endian order.
                byte[] ipBytesLE = (byte[])ipBytes.Clone();
                Array.Reverse(ipBytesLE);

                // Ensure the BigInteger treats the value as unsigned.
                if (ipBytesLE[ipBytesLE.Length - 1] >= 0x80)
                {
                    Array.Resize(ref ipBytesLE, ipBytesLE.Length + 1);
                    ipBytesLE[ipBytesLE.Length - 1] = 0;
                }
                return new BigInteger(ipBytesLE);
            }

            /// <summary>
            /// Converts a <see cref="BigInteger"/> value into an <see cref="IPAddress"/> of the specified bit length.
            /// </summary>
            /// <param name="value">The numeric value representing the IP address.</param>
            /// <param name="bitLength">The bit length of the address (32 for IPv4, 128 for IPv6).</param>
            /// <returns>The corresponding <see cref="IPAddress"/>.</returns>
            private static IPAddress BigIntegerToIPAddress(BigInteger value, int bitLength)
            {
                // Calculate the expected number of bytes.
                int numBytes = bitLength / 8;
                byte[] bytesLE = value.ToByteArray();

                // Prepare a byte array for the expected number of bytes.
                byte[] bytes = new byte[numBytes];
                for (int i = 0; i < numBytes; i++)
                {
                    bytes[i] = i < bytesLE.Length ? bytesLE[i] : (byte)0;
                }

                // Convert from little-endian to big-endian.
                Array.Reverse(bytes);
                return new IPAddress(bytes);
            }
        }
    }

}
