using System;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.IPAddressExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static BlackBytesBox.Routed.RequestFilters.Middleware.RemoteIPFilteringMiddleware;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on their protocol against configured allowed protocols.
    /// </summary>
    public class RemoteIPFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<RemoteIPFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<RemoteIPFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpProtocolFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public RemoteIPFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<RemoteIPFilteringMiddleware> logger, IOptionsMonitor<RemoteIPFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(RemoteIPFilteringMiddleware));
            });
        }

        public enum IpVersion
        {
            IPv4,
            IPv6,
            Unknown
        }

        public class IpInfo
        {
            /// <summary>
            /// The remote IP address as a string.
            /// </summary>
            public string RemoteIp { get; set; }
            /// <summary>
            /// The version of the IP address (IPv4 or IPv6).
            /// </summary>
            public IpVersion Version { get; set; }
        }

        /// <summary>
        /// Processes the HTTP request by validating its protocol and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;

            System.Net.IPAddress? remoteIpAddress = context.Connection.RemoteIpAddress;
            var ipInfo = remoteIpAddress.ToIpInfo();

            if (string.IsNullOrEmpty(ipInfo.RemoteIp))
            {
                _logger.LogError("Request rejected: Missing valid IP address. - aborting.");
                await context.Response.WriteDefaultStatusCodeAnswer(_optionsMonitor.CurrentValue.DisallowedStatusCode);
                return;
            }

            bool isAllowed = ipInfo.RemoteIp.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: RemoteIpAddress '{Protocol}' - continuing.", ipInfo.RemoteIp);
                context.SetItem("remoteIpAddressStr", ipInfo.RemoteIp);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    ipInfo.RemoteIp,
                    nameof(RemoteIPFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: RemoteIpAddress '{RemoteIpAddress}' - continuing.", ipInfo.RemoteIp);
                    context.Items["remoteIpAddressStr"] = ipInfo.RemoteIp;
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: RemoteIpAddress '{RemoteIpAddress}' - aborting.", ipInfo.RemoteIp);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}