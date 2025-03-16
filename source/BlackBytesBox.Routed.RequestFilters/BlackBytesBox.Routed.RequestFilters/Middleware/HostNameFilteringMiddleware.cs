using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    /// Middleware that filters HTTP requests based on their protocol against configured allowed protocols.
    /// </summary>
    public class HostNameFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<HostNameFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<HostNameFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

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

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(HostNameFilteringMiddleware));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its protocol and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task BlacklistAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            var host = context.Request.Host.Host;

            PatternMatchResult isWhitelist;
            PatternMatchResult isBlacklist;
            bool MatchedList = false;

            isWhitelist = StringUtility.MatchesAnyPattern(host, options.Whitelist, options.CaseSensitive);
            isBlacklist = StringUtility.MatchesAnyPattern(host, options.Blacklist, options.CaseSensitive);
            MatchedList = isWhitelist.IsMatch || isBlacklist.IsMatch;

            if (!MatchedList)
            {
                await NotMatchedAsync(context,options,host);
                return;
            }

            bool WhiteListFirst = false;
            if (options.FilterPriority.Equals("Whitelist", StringComparison.OrdinalIgnoreCase))
            {
                WhiteListFirst = true;
            }

            if (WhiteListFirst)
            {
                if (isWhitelist.IsMatch)
                {
                    await WhitelistedAsync(context);
                    return;
                }
                if (isBlacklist.IsMatch)
                {
                    await BlacklistedAsync(context);
                    return;
                }
            }
            else
            {
                if (isBlacklist.IsMatch)
                {
                    await BlacklistedAsync(context);
                    return;
                }
                if (isWhitelist.IsMatch)
                {
                    await WhitelistedAsync(context);
                    return;
                }
            }
        }

        public async Task NotMatchedAsync(HttpContext context, HostNameFilteringMiddlewareOptions options,string host)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                context.GetItem<string>("remoteIpAddressStr"),
                nameof(HostNameFilteringMiddleware),
                options.NotMatchedFailureRating,
                DateTime.UtcNow);

            if (options.NotMatchedContinue)
            {
                _logger.LogDebug("Blacklisted continue: host '{Host}' matched blacklisted pattern.", host);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogDebug("Blacklisted aborting: host '{Host}' matched blacklisted pattern {MatchedPattern}.", host, isBlacklist.MatchedPattern);
                await context.Response.WriteDefaultStatusCodeAnswer(options.NotMatchedStatusCode);
                return;
            }
        }

        public async Task BlacklistedAsync(HttpContext context)
        {
            await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
context.GetItem<string>("remoteIpAddressStr"),
nameof(HostNameFilteringMiddleware),
options.DisallowedFailureRating,
DateTime.UtcNow);

            if (options.ContinueOnDisallowed)
            {
                _logger.LogDebug("Blacklisted continue: host '{Host}' matched blacklisted pattern {MatchedPattern}.", host, isBlacklist.MatchedPattern);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogDebug("Blacklisted aborting: host '{Host}' matched blacklisted pattern {MatchedPattern}.", host, isBlacklist.MatchedPattern);
                await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                return;
            }
        }

        public async Task WhitelistedAsync(HttpContext context)
        {
            _logger.LogDebug("Whitelisted continue: host '{Host}' matched whitelisted pattern {MatchedPattern}", host, isWhitelist.MatchedPattern);
            await _nextMiddleware(context);
            return;
        }
    }
}
