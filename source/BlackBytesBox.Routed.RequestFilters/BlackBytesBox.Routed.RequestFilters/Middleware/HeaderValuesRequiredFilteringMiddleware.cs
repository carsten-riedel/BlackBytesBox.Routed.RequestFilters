using System;
using System.Linq;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that validates HTTP header values against configured whitelist and blacklist regex patterns.
    /// For each header configured in the options, the middleware checks its values and either allows or denies the request.
    /// </summary>
    public class HeaderValuesRequiredFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HeaderValuesRequiredFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HeaderValuesRequiredFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderValuesRequiredFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance used to record events.</param>
        /// <param name="optionsMonitor">The monitor providing middleware configuration options.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure metrics.</param>
        public HeaderValuesRequiredFilteringMiddleware(
            RequestDelegate nextMiddleware,
            ILogger<HeaderValuesRequiredFilteringMiddleware> logger,
            IOptionsMonitor<HeaderValuesRequiredFilteringMiddlewareOptions> optionsMonitor,
            MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(HeaderValuesRequiredFilteringMiddleware));
            });
        }

        /// <summary>
        /// Processes the HTTP request by evaluating header values against whitelist and blacklist regex patterns.
        /// If any header value fails the rule evaluation, the request is rejected (unless configured to continue).
        /// If at least one header passes its rule evaluation, the request is allowed.
        /// Otherwise, the request is denied.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            bool isAllowed = false;

            foreach (var item in options.Headers)
            {
                // Process only if the header is present.
                if (!context.Request.Headers.TryGetValue(item.Key, out var headerValues))
                {
                    isAllowed = false;
                    _logger.LogDebug("Missing required header: {HeaderName}", item.Key);
                    break;
                }

                var value = headerValues.FirstOrDefault();

                if (value == null)
                {
                    isAllowed = false;
                    _logger.LogDebug("Header '{HeaderName}' is present but has no value.", item.Key);
                    break;
                }

                isAllowed = value.MatchesAnyPattern(item.Value.Allowed);
                _logger.LogDebug("Processed header '{HeaderName}' with value '{HeaderValue}'.", item.Key, value);
            }

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: All required headers validated successfully. - continuing.");
                await _nextMiddleware(context);
                return;
            }
            else
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    context.GetItem<string>("remoteIpAddressStr"),
                    nameof(HeaderValuesRequiredFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: Missing or invalid header(s) - continuing.");
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: Missing or invalid header(s) - aborting.");
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
