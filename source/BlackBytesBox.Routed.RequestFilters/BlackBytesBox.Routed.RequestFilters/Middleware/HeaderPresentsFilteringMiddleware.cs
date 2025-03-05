using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using System.Linq;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that validates HTTP request headers against configured whitelist and blacklist regex patterns.
    /// The logic first identifies any blacklisted headers and then verifies if at least one header is explicitly allowed.
    /// </summary>
    public class HeaderPresentsFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HeaderPresentsFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HeaderPresentsFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderPresentsFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance used to record events.</param>
        /// <param name="optionsMonitor">The monitor providing middleware configuration options.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure metrics.</param>
        public HeaderPresentsFilteringMiddleware(
            RequestDelegate nextMiddleware,
            ILogger<HeaderPresentsFilteringMiddleware> logger,
            IOptionsMonitor<HeaderPresentsFilteringMiddlewareOptions> optionsMonitor,
            MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("HeaderPresentsFilteringMiddleware configuration has been updated.");
            });
        }

        /// <summary>
        /// Processes the HTTP request by evaluating header entries against blacklist and whitelist regex patterns.
        /// If any header matches a blacklisted pattern, the request is rejected (unless configured to continue).
        /// Otherwise, if at least one header matches the whitelist, the request is allowed.
        /// If no header qualifies, the request is denied.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            var headerKeys = context.Request.Headers.Keys.ToList();

            int allowedCount = 0;
            int blacklistedCount = 0;
            string? firstAllowedHeader = null;
            string? firstBlacklistedHeader = null;

            // Evaluate each header using regex checks for whitelist and blacklist.
            foreach (var headerKey in headerKeys)
            {
                if (options.Blacklist != null && ValidationsStringExtensions.MatchesAnyPattern(headerKey, options.Blacklist))
                {
                    blacklistedCount++;
                    firstBlacklistedHeader ??= headerKey;
                }
                if (options.Whitelist != null && ValidationsStringExtensions.MatchesAnyPattern(headerKey, options.Whitelist))
                {
                    allowedCount++;
                    firstAllowedHeader ??= headerKey;
                }
            }

            // Decision logic: Block if any header is blacklisted.
            if (blacklistedCount > 0)
            {
                _logger.LogDebug("Request blocked: {BlacklistedCount} header(s) matched blacklisted patterns, e.g. '{Header}'.", blacklistedCount, firstBlacklistedHeader);
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Unable to determine client's IP address for header validation.");
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(HeaderPresentsFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Continuing request processing despite blacklisted header due to configuration.");
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }

            // Allow the request if any header explicitly matches the whitelist.
            if (allowedCount > 0)
            {
                _logger.LogDebug("Request allowed: {AllowedCount} header(s) matched whitelisted patterns, e.g. '{Header}'.", allowedCount, firstAllowedHeader);
                await _nextMiddleware(context);
                return;
            }

            // If no header qualifies, deny the request.
            _logger.LogDebug("Request denied: No header matched the allowed patterns.");
            await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
        }
    }
}
