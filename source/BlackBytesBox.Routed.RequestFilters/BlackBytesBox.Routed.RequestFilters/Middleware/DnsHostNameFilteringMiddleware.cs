using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using System.Net;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;

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

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Config updated: {Options}", nameof(DnsHostNameFilteringMiddlewareOptions));
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
            string? remoteIPAddress = context.Connection.RemoteIpAddress?.ToString();
            if (remoteIPAddress == null)
            {
                _logger.LogError("Rejected: no IP.");
                await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                return;
            }

            string? resolvedDnsName = null;
            try
            {
                var hostEntry = Dns.GetHostEntry(remoteIPAddress);
                resolvedDnsName = hostEntry.HostName;
            }
            catch (Exception)
            {
                resolvedDnsName = null;
            }

            bool isAllowed = resolvedDnsName != null && resolvedDnsName.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: DNS host '{DnsHost}'.", resolvedDnsName);
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
                    _logger.LogDebug("Disallowed: DNS host '{DnsHost}' - continuing.",resolvedDnsName);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: DNS host '{DnsHost}' - aborting.",resolvedDnsName);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
