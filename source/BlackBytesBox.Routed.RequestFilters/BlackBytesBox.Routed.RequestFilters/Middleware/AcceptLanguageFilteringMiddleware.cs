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
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(AcceptLanguageFilteringMiddlewareOptions));
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
            // Retrieve current configuration options.
            var options = _optionsMonitor.CurrentValue;

            // Extract the first value of the Accept-Language header (if present).
            string? languageHeader = context.Request.Headers.AcceptLanguage.FirstOrDefault()?.ToString();
            languageHeader ??= string.Empty;

            // Validate the header against the configured whitelist and blacklist patterns.
            var isAllowed = languageHeader.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);

            if (isAllowed)
            {
                _logger.LogDebug("Accept-Language header '{AcceptLanguage}' is allowed.", languageHeader);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                // Retrieve the client's IP address for logging purposes.
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Missing valid IP address.");
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                // Record the failure event using the middleware failure point service.
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(AcceptLanguageFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Accept-Language header did not meet criteria in {MiddlewareName}, but processing will continue as configured.", nameof(AcceptLanguageFilteringMiddleware));
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Access denied: Accept-Language header '{AcceptLanguage}' is not allowed.", languageHeader);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
