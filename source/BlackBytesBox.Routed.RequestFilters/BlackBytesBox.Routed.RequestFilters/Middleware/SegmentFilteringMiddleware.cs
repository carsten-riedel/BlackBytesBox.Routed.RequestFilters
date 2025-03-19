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
        public SegmentFilteringMiddleware(RequestDelegate nextMiddleware,ILogger<SegmentFilteringMiddleware> logger, IOptionsMonitor<SegmentFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
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
                    _logger.LogDebug("Unreadable continue: Unable to parse display URL '{DisplayUrl}'.", context.Request.GetDisplayUrl());
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Unreadable aborting: Unable to parse display URL '{DisplayUrl}'.", context.Request.GetDisplayUrl());
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
                    firstBlacklistedSegment ??= isBlacklist;
                    break;
                }
            }

            foreach (var segment in trimmedSegments)
            {
                PatternMatchResult isWhitelist = segment.MatchesAnyPatternNew(options.Whitelist, !options.CaseSensitive);

                if (isWhitelist.IsMatch)
                {
                    whitelistedCount++;
                    firstWhitelistedSegment ??= isWhitelist;
                    break;
                }
            }

            bool NotMatched = (!firstWhitelistedSegment.IsMatch && !firstBlacklistedSegment.IsMatch);
            // Check if the host matches any of the configured patterns.
            if (NotMatched)
            {
                await NotMatchedAsync(context, options, trimmedSegments);
                return;
            }

            // Use pre-calculated filter priority to decide branch.
            if (_whitelistFirst)
            {
                if (firstWhitelistedSegment.IsMatch)
                {
                    await WhitelistedAsync(context, options, trimmedSegments, firstWhitelistedSegment);
                    return;
                }
                if (firstBlacklistedSegment.IsMatch)
                {
                    await BlacklistedAsync(context, options, trimmedSegments, firstBlacklistedSegment);
                    return;
                }
            }
            else
            {
                if (firstBlacklistedSegment.IsMatch)
                {
                    await BlacklistedAsync(context, options, trimmedSegments, firstBlacklistedSegment);
                    return;
                }
                if (firstWhitelistedSegment.IsMatch)
                {
                    await WhitelistedAsync(context, options, trimmedSegments, firstWhitelistedSegment);
                    return;
                }
            }


            //// Decision logic: Block if any segment is blacklisted.
            //if (blacklistedCount > 0)
            //{
            //    await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
            //        context.GetItem<string>("remoteIpAddressStr"),
            //        nameof(SegmentFilteringMiddleware),
            //        options.DisallowedFailureRating,
            //        DateTime.UtcNow);

            //    if (options.ContinueOnDisallowed)
            //    {
            //        _logger.LogDebug("Disallowed: Segment {BlacklistedCount} no match  e.g. '{Segment}' - continuing.", blacklistedCount, firstBlacklistedSegment);
            //        await _nextMiddleware(context);
            //        return;
            //    }
            //    else
            //    {
            //        _logger.LogDebug("Disallowed: Segment {BlacklistedCount} no match  e.g. '{Segment}' - aborting.", blacklistedCount, firstBlacklistedSegment);
            //        await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
            //        return;
            //    }
            //}

            //// Allow the request if at least one segment explicitly matches the whitelist.
            //if (allowedCount > 0)
            //{
            //    _logger.LogDebug("Allowed: Segment {AllowedCount} segment(s) matched patterns, e.g. '{Segment}'. - continuing.", allowedCount, firstWhitelistedSegment);
            //    await _nextMiddleware(context);
            //    return;
            //}

            //// If no segment qualifies, deny the request.
            //_logger.LogDebug("Disallowed: Segment no allowed or disallowed patterns.- aborting.");
            //await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
        }
    }
}