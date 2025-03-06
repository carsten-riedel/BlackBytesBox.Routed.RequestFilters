using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using System.Linq;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on the User-Agent header against configured allowed values.
    /// </summary>
    public class UserAgentFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<UserAgentFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<UserAgentFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserAgentFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance for recording middleware operations.</param>
        /// <param name="optionsMonitor">The options monitor for retrieving the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points when filtering fails.</param>
        public UserAgentFilteringMiddleware(RequestDelegate nextMiddleware,  ILogger<UserAgentFilteringMiddleware> logger, IOptionsMonitor<UserAgentFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(UserAgentFilteringMiddlewareOptions));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its User-Agent header and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            string? userAgentHeader = context.Request.Headers.UserAgent.FirstOrDefault()?.ToString();
            userAgentHeader ??= string.Empty;

            bool isAllowed = userAgentHeader.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("User-Agent '{UserAgent}' is permitted.", userAgentHeader);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Missing valid IP address.");
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(UserAgentFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("User-Agent '{UserAgent}' not allowed in {MiddlewareName}; continuing.", userAgentHeader, nameof(UserAgentFilteringMiddleware));
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("User-Agent '{UserAgent}' is not permitted. Responding with status code {StatusCode}.", userAgentHeader, options.DisallowedStatusCode);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
