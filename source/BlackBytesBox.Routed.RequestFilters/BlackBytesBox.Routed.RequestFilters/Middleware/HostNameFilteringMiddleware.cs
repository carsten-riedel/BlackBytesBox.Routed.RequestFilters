using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.DictionaryExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Utility.StringUtility;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static BlackBytesBox.Routed.RequestFilters.Utility.StringUtility.StringUtility;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on their host name against configured allowed patterns.
    /// </summary>
    public class HostNameFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HostNameFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HostNameFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        // Private property to store the filter priority.
        private bool _whitelistFirst;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostNameFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public HostNameFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<HostNameFilteringMiddleware> logger, IOptionsMonitor<HostNameFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            // Pre-calculate filter priority based on current options.
            _whitelistFirst = _optionsMonitor.CurrentValue.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);

            // Update filter priority when configuration changes.
            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(HostNameFilteringMiddleware));
                _whitelistFirst = updatedOptions.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its host name against configured patterns.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            var host = context.Request.Host.Host;

            PatternMatchResult isWhitelist = host.MatchesAnyPatternNew(options.Whitelist, options.CaseSensitive);
            PatternMatchResult isBlacklist = host.MatchesAnyPatternNew(options.Blacklist, options.CaseSensitive);

            // Check if the host matches any of the configured patterns.
            if (!isWhitelist.IsMatch && !isBlacklist.IsMatch)
            {
                await NotMatchedAsync(context, options, host);
                return;
            }

            // Use pre-calculated filter priority to decide branch.
            if (_whitelistFirst)
            {
                if (isWhitelist.IsMatch)
                {
                    await WhitelistedAsync(context, options, host, isWhitelist);
                    return;
                }
                if (isBlacklist.IsMatch)
                {
                    await BlacklistedAsync(context, options, host, isBlacklist);
                    return;
                }
            }
            else
            {
                if (isBlacklist.IsMatch)
                {
                    await BlacklistedAsync(context, options, host, isBlacklist);
                    return;
                }
                if (isWhitelist.IsMatch)
                {
                    await WhitelistedAsync(context, options, host, isWhitelist);
                    return;
                }
            }
        }

        /// <summary>
        /// Handles requests that did not match any whitelist or blacklist patterns.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <param name="options">The middleware options.</param>
        /// <param name="host">The host name extracted from the request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotMatchedAsync(HttpContext context, HostNameFilteringMiddlewareOptions options, string host)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(HostNameFilteringMiddleware),
                options.NotMatchedFailureRating,
                DateTime.UtcNow);

            
            if (options.NotMatchedContinue)
            {
                _logger.Log(options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,"NotMatched continue: host '{Host}' did not match any whitelist or blacklist patterns. Continuing request processing.",host);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.Log(options.NotMatchedLogWarning ? LogLevel.Warning : LogLevel.Debug,"NotMatched aborting: host '{Host}' did not match any whitelist or blacklist patterns. Aborting request processing.", host);
                await context.Response.WriteDefaultStatusCodeAnswer(options.NotMatchedStatusCode);
                return;
            }
        }

        /// <summary>
        /// Handles requests that match a blacklisted pattern.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <param name="options">The middleware options.</param>
        /// <param name="host">The host name extracted from the request.</param>
        /// <param name="patternMatchResult">The result of the pattern matching.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BlacklistedAsync(HttpContext context, HostNameFilteringMiddlewareOptions options, string host, PatternMatchResult patternMatchResult)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(HostNameFilteringMiddleware),
                options.BlacklistFailureRating,
                DateTime.UtcNow);

            if (options.BlacklistContinue)
            {
                _logger.LogDebug("Blacklisted continue: host '{Host}' matched blacklisted pattern {MatchedPattern}.", host, patternMatchResult.MatchedPattern);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogDebug("Blacklisted aborting: host '{Host}' matched blacklisted pattern {MatchedPattern}.", host, patternMatchResult.MatchedPattern);
                await context.Response.WriteDefaultStatusCodeAnswer(options.BlacklistStatusCode);
                return;
            }
        }

        /// <summary>
        /// Handles requests that match a whitelisted pattern.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <param name="options">The middleware options.</param>
        /// <param name="host">The host name extracted from the request.</param>
        /// <param name="patternMatchResult">The result of the pattern matching.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task WhitelistedAsync(HttpContext context, HostNameFilteringMiddlewareOptions options, string host, PatternMatchResult patternMatchResult)
        {
            _logger.LogDebug("Whitelisted continue: host '{Host}' matched whitelisted pattern {MatchedPattern}.", host, patternMatchResult.MatchedPattern);
            await _nextMiddleware(context);
            return;
        }
    }
}
