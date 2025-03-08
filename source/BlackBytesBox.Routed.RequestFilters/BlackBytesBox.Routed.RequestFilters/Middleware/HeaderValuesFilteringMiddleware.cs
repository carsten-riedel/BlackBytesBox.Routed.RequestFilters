using System;
using System.Linq;
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
    /// Middleware that validates HTTP header values against configured whitelist and blacklist regex patterns.
    /// For each header configured in the options, the middleware checks its values and either allows or denies the request.
    /// </summary>
    public class HeaderValuesFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HeaderValuesFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HeaderValuesFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderValuesFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance used to record events.</param>
        /// <param name="optionsMonitor">The monitor providing middleware configuration options.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure metrics.</param>
        public HeaderValuesFilteringMiddleware(
            RequestDelegate nextMiddleware,
            ILogger<HeaderValuesFilteringMiddleware> logger,
            IOptionsMonitor<HeaderValuesFilteringMiddlewareOptions> optionsMonitor,
            MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(HeaderValuesFilteringMiddlewareOptions));
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
            int allowedCount = 0;
            int blacklistedCount = 0;
            string? firstAllowedHeader = null;
            string? firstBlacklistedHeader = null;

            // Iterate over each configured header rule.
            foreach (var ruleEntry in options.Headers)
            {
                string headerName = ruleEntry.Key;
                var rule = ruleEntry.Value;

                // Skip if the rule is null.
                if (rule == null)
                    continue;

                // Determine if whitelist and/or blacklist are defined.
                bool hasWhitelist = rule.Whitelist != null && rule.Whitelist.Any();
                bool hasBlacklist = rule.Blacklist != null && rule.Blacklist.Any();

                // If neither is defined, skip filtering for this header.
                if (!hasWhitelist && !hasBlacklist)
                    continue;

                // Only process if the header is present.
                if (!context.Request.Headers.TryGetValue(headerName, out var headerValues))
                    continue;

                bool headerDisallowed = false;
                bool headerAllowed = false;

                // Evaluate each header value.
                foreach (var value in headerValues)
                {
                    // If blacklist is defined and any value matches, mark disallowed.
                    if (hasBlacklist && value.MatchesAnyPattern(rule.Blacklist))
                    {
                        headerDisallowed = true;
                        break; // Immediate block for this header.
                    }

                    // If whitelist is defined and a value matches, mark allowed.
                    if (hasWhitelist && value.MatchesAnyPattern(rule.Whitelist))
                    {
                        headerAllowed = true;
                    }
                }

                // Determine outcome based on which rules are defined.
                bool ruleAllows = false;
                if (hasWhitelist && !hasBlacklist)
                {
                    // With only a whitelist, header is allowed only if a value matches.
                    ruleAllows = headerAllowed;
                }
                else if (hasBlacklist && !hasWhitelist)
                {
                    // With only a blacklist, header is allowed as long as no value was disallowed.
                    ruleAllows = !headerDisallowed;
                }
                else if (hasWhitelist && hasBlacklist)
                {
                    // With both, header is allowed if at least one value matches whitelist and none match blacklist.
                    ruleAllows = headerAllowed && !headerDisallowed;
                }

                // Tally results based on outcome.
                if (!ruleAllows)
                {
                    blacklistedCount++;
                    firstBlacklistedHeader ??= headerName;
                }
                else
                {
                    allowedCount++;
                    firstAllowedHeader ??= headerName;
                }
            }

            // Global decision logic.
            if (blacklistedCount > 0)
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    context.GetItem<string>("remoteIpAddressStr"),
                    nameof(HeaderValuesFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: {BlacklistedCount} header rules failed, e.g. '{Header}'. Continuing.", blacklistedCount, firstBlacklistedHeader);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: {BlacklistedCount} header rules failed, e.g. '{Header}'. Aborting.", blacklistedCount, firstBlacklistedHeader);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }

            if (allowedCount > 0)
            {
                _logger.LogDebug("Allowed: {AllowedCount} header rules passed, e.g. '{Header}'.", allowedCount, firstAllowedHeader);
                await _nextMiddleware(context);
                return;
            }

            _logger.LogDebug("Request denied: No header value passed the allowed rules.");
            await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
        }
    }
}
