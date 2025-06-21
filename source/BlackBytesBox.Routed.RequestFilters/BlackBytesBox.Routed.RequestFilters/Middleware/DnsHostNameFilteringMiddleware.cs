using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using DnsClient;
using DnsClient.Protocol;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on DNS host name against configured allowed values.
    /// </summary>
    public class DnsHostNameFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<DnsHostNameFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<DnsHostNameFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;
        private readonly LookupClient _lookupClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsHostNameFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public DnsHostNameFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<DnsHostNameFilteringMiddleware> logger, IOptionsMonitor<DnsHostNameFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            var publicServers = new[]
            {
                // Google IPv4
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Parse("8.8.4.4"),

                // Google IPv6
                IPAddress.Parse("2001:4860:4860::8888"),
                IPAddress.Parse("2001:4860:4860::8844"),

                // Cloudflare IPv4
                IPAddress.Parse("1.1.1.1"),
                IPAddress.Parse("1.0.0.1"),

                // Cloudflare IPv6
                IPAddress.Parse("2606:4700:4700::1111"),
                IPAddress.Parse("2606:4700:4700::1001")
            };

            var options = new LookupClientOptions(publicServers)
            {
                AutoResolveNameServers = true,   // include your OS-configured servers
                UseRandomNameServer = false,  // stick to list order
                ThrowDnsErrors = false,  // don’t throw, just fail over
                Timeout = TimeSpan.FromSeconds(2),
                Retries = 0,      // try each server exactly once
                UseCache = true
            };

            _lookupClient = new LookupClient(options);

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(DnsHostNameFilteringMiddleware));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its DNS host name and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;

            var remoteIPAddress = context.GetItem<string>("remoteIpAddressStr");

            string? resolvedDnsName = null;

            if (IPAddress.TryParse(remoteIPAddress, out var ip))
            {
                try
                {
                    // Reverse‐lookup via Google DNS
                    var result = await _lookupClient.QueryReverseAsync(ip);
                    var ptr = result
                        .Answers                   // this is IEnumerable<DnsResourceRecord>
                        .PtrRecords()              // extension method lives here
                        .FirstOrDefault()
                        ?.PtrDomainName
                        .Value;
                    resolvedDnsName = ptr;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reverse DNS lookup failed for {IP}", ip);
                }
            }

            bool isAllowed = false;
            bool mayBeClient = false;
            if (resolvedDnsName != null)
            {
                _logger.LogDebug("Resolved DNS host: {DnsHost}", resolvedDnsName);
                isAllowed = resolvedDnsName.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);
            }
            else
            {
                _logger.LogDebug("No DNS host resolved for IP: {IP}", remoteIPAddress);
                mayBeClient = true; // If no DNS name is resolved, we assume it might be a client
            }

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: DNS host '{DnsHost}' - continuing.", resolvedDnsName);
                await _nextMiddleware(context);
                return;
            }
            else if (mayBeClient)
            {
                _logger.LogDebug("May be client: DNS host '{DnsHost}' - continuing.", resolvedDnsName);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    remoteIPAddress,
                    nameof(DnsHostNameFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: DNS host '{DnsHost}' - continuing.", resolvedDnsName);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: DNS host '{DnsHost}' - aborting.", resolvedDnsName);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
