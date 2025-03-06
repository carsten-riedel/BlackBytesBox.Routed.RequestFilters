using System;
using System.Linq;
using System.Threading.Tasks;

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
    /// Middleware that filters HTTP requests by validating the <c>Accept-Language</c> header 
    /// against configured whitelist and blacklist patterns.
    /// </summary>
    public class AcceptLanguageFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<AcceptLanguageFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<AcceptLanguageFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcceptLanguageFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance used for logging middleware events.</param>
        /// <param name="optionsMonitor">The monitor for retrieving configuration settings for the middleware.</param>
        /// <param name="middlewareFailurePointService">
        /// The service used for tracking and recording failure points when requests do not meet configured criteria.
        /// </param>
        public AcceptLanguageFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<AcceptLanguageFilteringMiddleware> logger, IOptionsMonitor<AcceptLanguageFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Config updated: {Options}", nameof(AcceptLanguageFilteringMiddlewareOptions));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating the <c>Accept-Language</c> header against the configured whitelist and blacklist.
        /// If the header is allowed, the request is forwarded; otherwise, the request is either blocked or allowed to continue based on configuration.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            string languageHeader = context.Request.Headers.AcceptLanguage.FirstOrDefault()?.ToString() ?? string.Empty;

            bool isAllowed = languageHeader.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: Accept-Language '{Lang}'.", languageHeader);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Rejected: no IP for Accept-Language '{Lang}'.", languageHeader);
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(AcceptLanguageFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: Accept-Language '{Lang}' - continuing.",languageHeader);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: Accept-Language '{Lang}' - aborting.", languageHeader);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
