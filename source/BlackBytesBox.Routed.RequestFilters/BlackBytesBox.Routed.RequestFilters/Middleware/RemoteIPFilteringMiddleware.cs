using System;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(RemoteIPFilteringMiddlewareOptions));
            });
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
            if (remoteIpAddress == null)
            {
                _logger.LogError("Request rejected: Missing valid IP address.");
                await context.Response.WriteDefaultStatusCodeAnswer(_optionsMonitor.CurrentValue.DisallowedStatusCode);
                return;
            }

            string remoteIpAddressStr = remoteIpAddress.ToString();

            bool isAllowed = remoteIpAddressStr.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);
            
            if (isAllowed)
            {
                _logger.LogDebug("Allowed: RemoteIpAddress '{Protocol}'.", remoteIpAddressStr);
                context.SetItem("remoteIpAddressStr", remoteIpAddressStr);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    remoteIpAddressStr,
                    nameof(RemoteIPFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: RemoteIpAddress '{RemoteIpAddress}' - continuing.", remoteIpAddressStr);
                    context.Items["remoteIpAddressStr"] = remoteIpAddressStr;
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: RemoteIpAddress '{RemoteIpAddress}' - aborting.", remoteIpAddressStr);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }




        }
    }
}