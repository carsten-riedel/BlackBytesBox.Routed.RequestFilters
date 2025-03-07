using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on their protocol against configured allowed protocols.
    /// </summary>
    public class HttpProtocolFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HttpProtocolFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HttpProtocolFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpProtocolFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public HttpProtocolFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<HttpProtocolFilteringMiddleware> logger, IOptionsMonitor<HttpProtocolFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Config updated: {Options}", nameof(HttpProtocolFilteringMiddlewareOptions));
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
            string protocol = context.Request.Protocol;

            bool isAllowed = protocol.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: protocol '{Protocol}'.", protocol);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Rejected: no IP for protocol '{Protocol}'.", protocol);
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(HttpProtocolFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: protocol '{Protocol}' - continuing.", protocol);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: protocol '{Protocol}' - aborting.", protocol);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
