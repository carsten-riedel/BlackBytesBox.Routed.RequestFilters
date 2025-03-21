using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using System.Linq;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using Microsoft.AspNetCore.Http.Extensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Utility.HttpContextUtility;
using Microsoft.Extensions.Hosting;
using static BlackBytesBox.Routed.RequestFilters.Utility.StringUtility.StringUtility;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on the URI segments of the request URL
    /// against configured whitelist and blacklist patterns.
    /// </summary>
    public class SegmentFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<SegmentFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<SegmentFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        // Private property to store the filter priority.
        private bool _whitelistFirst;

        /// <summary>
        /// Initializes a new instance of the <see cref="SegmentFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance for recording middleware operations.</param>
        /// <param name="optionsMonitor">The monitor for retrieving the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points when filtering fails.</param>
        public SegmentFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<SegmentFilteringMiddleware> logger, IOptionsMonitor<SegmentFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            // Pre-calculate filter priority based on current options.
            _whitelistFirst = _optionsMonitor.CurrentValue.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(SegmentFilteringMiddleware));
                _whitelistFirst = updatedOptions.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating the URI segments of the request URL
        /// against the configured whitelist and blacklist patterns.
        /// If any segment is blacklisted or the full URI cannot be built, the request is treated as not allowed.
        /// If at least one segment is whitelisted, the request is forwarded.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            var fullUri = HttpContextUtility.GetUriFromRequestDisplayUrl(context, _logger);
            var displayUrl = context.Request.GetDisplayUrl();

            // If fullUri is null, treat it as not allowed.
            if (fullUri == null)
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    context.GetItem<string>("remoteIpAddressStr"),
                    nameof(SegmentFilteringMiddleware),
                    options.UnreadableFailureRating,
                    DateTime.UtcNow);

                if (options.UnreadableContinue)
                {
                    _logger.LogDebug("Unreadable continue: Unable to parse display URL '{DisplayUrl}'.", displayUrl);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Unreadable aborting: Unable to parse display URL '{DisplayUrl}'.", displayUrl);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.UnreadableStatusCode);
                    return;
                }
            }

            int whitelistedCount = 0;
            int blacklistedCount = 0;
            PatternMatchResult firstWhitelistedSegment = new PatternMatchResult();
            PatternMatchResult firstBlacklistedSegment = new PatternMatchResult();

            string[]? trimmedSegments = fullUri.Segments.Select(s => Uri.UnescapeDataString(s.Trim('/'))).ToArray();

            foreach (var segment in trimmedSegments)
            {
                PatternMatchResult isBlacklist = segment.MatchesAnyPatternNew(options.Blacklist, !options.CaseSensitive);

                if (isBlacklist.IsMatch)
                {
                    blacklistedCount++;
                    firstBlacklistedSegment = isBlacklist;
                    break;
                }
            }

            foreach (var segment in trimmedSegments)
            {
                PatternMatchResult isWhitelist = segment.MatchesAnyPatternNew(options.Whitelist, !options.CaseSensitive);

                if (isWhitelist.IsMatch)
                {
                    whitelistedCount++;
                    firstWhitelistedSegment = isWhitelist;
                    break;
                }
            }

            bool NotMatched = (!firstWhitelistedSegment.IsMatch && !firstBlacklistedSegment.IsMatch);
            // Check if the host matches any of the configured patterns.
            if (NotMatched)
            {
                await NotMatchedAsync(context, options, displayUrl);
                return;
            }

            // Use pre-calculated filter priority to decide branch.
            if (_whitelistFirst)
            {
                if (firstWhitelistedSegment.IsMatch)
                {
                    await WhitelistedAsync(context, options, displayUrl, firstWhitelistedSegment);
                    return;
                }
                if (firstBlacklistedSegment.IsMatch)
                {
                    await BlacklistedAsync(context, options, displayUrl, firstBlacklistedSegment);
                    return;
                }
            }
            else
            {
                if (firstBlacklistedSegment.IsMatch)
                {
                    await BlacklistedAsync(context, options, displayUrl, firstBlacklistedSegment);
                    return;
                }
                if (firstWhitelistedSegment.IsMatch)
                {
                    await WhitelistedAsync(context, options, displayUrl, firstWhitelistedSegment);
                    return;
                }
            }
        }

        /// <summary>
        /// Asynchronously handles requests for which the URL (displayUrl) does not match any whitelist or blacklist patterns.
        /// </summary>
        /// <remarks>
        /// Depending on the options provided, this method logs the event and either continues processing the request
        /// or aborts it by writing a default status code response. The <paramref name="displayUrl"/> parameter is used to log
        /// the URL segments that were derived from the request path.
        /// </remarks>
        /// <param name="context">The current HTTP context of the request.</param>
        /// <param name="options">The configuration options for segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>
        /// <code>
        /// // Example usage:
        /// await NotMatchedAsync(httpContext, middlewareOptions, "/segment1/segment2/segment3");
        /// </code>
        /// </example>
        public async Task NotMatchedAsync(HttpContext context, SegmentFilteringMiddlewareOptions options, string displayUrl)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(SegmentFilteringMiddleware),
                options.NotMatchedFailureRating,
                DateTime.UtcNow);

            if (options.NotMatchedContinue)
            {
                _logger.Log(
                    options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,
                    "NotMatched continue: URL segments '{Segments}' did not match any whitelist or blacklist patterns.",
                    displayUrl);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.Log(
                    options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,
                    "NotMatched aborting: URL segments '{Segments}' did not match any whitelist or blacklist patterns.",
                    displayUrl);
                await context.Response.WriteDefaultStatusCodeAnswer(options.NotMatchedStatusCode);
                return;
            }
        }

        /// <summary>
        /// Asynchronously handles requests that match a blacklisted URL pattern.
        /// </summary>
        /// <remarks>
        /// Depending on the provided options, this method logs the event and either continues processing the request
        /// or aborts it by writing a default status code response. The <paramref name="displayUrl"/> parameter is used to log
        /// the URL segments that triggered the blacklist rule.
        /// </remarks>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="options">The configuration options for the segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <param name="firstBlacklistedSegment">The first matching blacklisted pattern result.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task BlacklistedAsync(HttpContext context, SegmentFilteringMiddlewareOptions options, string displayUrl, PatternMatchResult firstBlacklistedSegment)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(SegmentFilteringMiddleware),
                options.BlacklistFailureRating,
                DateTime.UtcNow);

            if (options.BlacklistContinue)
            {
                _logger.LogDebug("Blacklisted continue: URL segments '{Segments}' matched blacklisted pattern {MatchedPattern}.", displayUrl, firstBlacklistedSegment.MatchedPattern);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogDebug("Blacklisted aborting: URL segments '{Segments}' matched blacklisted pattern {MatchedPattern}.", displayUrl, firstBlacklistedSegment.MatchedPattern);
                await context.Response.WriteDefaultStatusCodeAnswer(options.BlacklistStatusCode);
                return;
            }
        }

        /// <summary>
        /// Asynchronously handles requests that match a whitelisted URL pattern by continuing the middleware pipeline.
        /// </summary>
        /// <remarks>
        /// This method logs the event using the <paramref name="displayUrl"/> parameter and then continues processing the request
        /// by calling the next middleware.
        /// </remarks>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="options">The configuration options for the segment filtering middleware.</param>
        /// <param name="displayUrl">A string representing the combined URL segments (e.g., "/segment1/segment2/segment3").</param>
        /// <param name="firstWhitelistedSegment">The first matching whitelisted pattern result.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WhitelistedAsync(HttpContext context, SegmentFilteringMiddlewareOptions options, string displayUrl, PatternMatchResult firstWhitelistedSegment)
        {
            _logger.LogDebug("Whitelisted continue: URL segments '{Segments}' matched whitelisted pattern {MatchedPattern}.", displayUrl, firstWhitelistedSegment.MatchedPattern);
            await _nextMiddleware(context);
            return;
        }

    }
}