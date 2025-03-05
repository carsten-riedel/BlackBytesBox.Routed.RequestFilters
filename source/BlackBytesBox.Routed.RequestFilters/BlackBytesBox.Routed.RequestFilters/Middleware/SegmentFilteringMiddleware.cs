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

        /// <summary>
        /// Initializes a new instance of the <see cref="SegmentFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance for recording middleware operations.</param>
        /// <param name="optionsMonitor">The monitor for retrieving the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points when filtering fails.</param>
        public SegmentFilteringMiddleware(
            RequestDelegate nextMiddleware,
            ILogger<SegmentFilteringMiddleware> logger,
            IOptionsMonitor<SegmentFilteringMiddlewareOptions> optionsMonitor,
            MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(SegmentFilteringMiddlewareOptions));
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
            var fullUri = GetFullRequestUri(context);

            // If fullUri is null, treat it as not allowed.
            if (fullUri == null)
            {
                _logger.LogError("Failed to build full request URI. Defaulting to request denial.");
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Unable to determine client's IP address for URI segment validation.");
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(SegmentFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Continuing request processing despite URI build failure, per configuration.");
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }

            int allowedCount = 0;
            int blacklistedCount = 0;
            string? firstAllowedSegment = null;
            string? firstBlacklistedSegment = null;

            // Evaluate each URI segment using regex checks for whitelist and blacklist.
            foreach (var segment in fullUri.Segments)
            {
                string trimmedSegment = Uri.UnescapeDataString(segment.Trim('/'));

                if (options.Blacklist != null && ValidationsStringExtensions.MatchesAnyPattern(trimmedSegment, options.Blacklist))
                {
                    blacklistedCount++;
                    firstBlacklistedSegment ??= trimmedSegment;
                }
                if (options.Whitelist != null && ValidationsStringExtensions.MatchesAnyPattern(trimmedSegment, options.Whitelist))
                {
                    allowedCount++;
                    firstAllowedSegment ??= trimmedSegment;
                }
            }

            // Decision logic: Block if any segment is blacklisted.
            if (blacklistedCount > 0)
            {
                _logger.LogDebug("Request blocked: {BlacklistedCount} segment(s) matched blacklisted patterns, e.g. '{Segment}'.", blacklistedCount, firstBlacklistedSegment);
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Unable to determine client's IP address for URI segment validation.");
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(SegmentFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Continuing request processing despite blacklisted segment, per configuration.");
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }

            // Allow the request if at least one segment explicitly matches the whitelist.
            if (allowedCount > 0)
            {
                _logger.LogDebug("Request allowed: {AllowedCount} segment(s) matched whitelisted patterns, e.g. '{Segment}'.", allowedCount, firstAllowedSegment);
                await _nextMiddleware(context);
                return;
            }

            // If no segment qualifies, deny the request.
            _logger.LogDebug("Request denied: No segment matched the allowed patterns.");
            await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
        }

        /// <summary>
        /// Builds the complete URI from the request components.
        /// </summary>
        /// <param name="context">The HTTP context containing the request.</param>
        /// <returns>
        /// The full URI of the request if successfully constructed; otherwise, <c>null</c>.
        /// </returns>
        public Uri? GetFullRequestUri(HttpContext context)
        {
            try
            {
                var request = context.Request;

                // Validate the essential components of the request URI.
                if (string.IsNullOrEmpty(request.Scheme) || string.IsNullOrEmpty(request.Host.Host))
                {
                    _logger.LogError("Invalid request: Missing scheme or host.");
                    return null;
                }

                // Build the full URI from the request's scheme, host, port, path, and query string.
                var uriBuilder = new UriBuilder
                {
                    Scheme = request.Scheme,
                    Host = request.Host.Host,
                    Port = request.Host.Port ?? -1, // Use default port if not specified.
                    Path = request.PathBase.Add(request.Path).ToString(),
                    Query = request.QueryString.ToString()
                };

                return uriBuilder.Uri;
            }
            catch (Exception ex)
            {
                // Log the exception and return null so filtering logic treats the request as not allowed.
                _logger.LogDebug(ex, "Failed to build full request URI. {DisplayUrl}", context.Request.GetDisplayUrl());
                return null;
            }
        }
    }
}
