﻿using System;
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
    /// Middleware that filters HTTP requests based on their protocol against configured allowed protocols.
    /// </summary>
    public class PathDeepFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<PathDeepFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<PathDeepFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostNameFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public PathDeepFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<PathDeepFilteringMiddleware> logger, IOptionsMonitor<PathDeepFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(PathDeepFilteringMiddlewareOptions));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its protocol and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;

            var requestPath = context.Request.Path.ToString();
            var pathDepth = CalculatePathDepth(requestPath);

            var isAllowed = options.PathDeepLimit >= pathDepth;

            // If the path depth is within allowed limits, continue processing.
            if (isAllowed)
            {
                _logger.LogDebug("Pathdeep: a path deep of {pathDepth} is allowed for request path {requestPath}.", pathDepth, requestPath);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                string? requestIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(requestIp))
                {
                    _logger.LogError("Request rejected: Missing valid IP address for request path {requestPath}.", requestPath);
                    await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                    return;
                }

                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                    requestIp,
                    nameof(PathDeepFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Request did not meet protocol criteria in {MiddlewareName} for request path {requestPath}, but processing will continue as configured.", nameof(PathDeepFilteringMiddleware), requestPath);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Pathdeep: a path deep of {pathDepth} is not allowed for request path {requestPath}.", pathDepth, requestPath);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }

        private int CalculatePathDepth(string path)
        {
            var normalizedPath =path.TrimSpecific('/', 1, 1);
            if (String.IsNullOrEmpty(normalizedPath))
            {
                return 0;
            }
            var res = normalizedPath.Split('/', StringSplitOptions.None);
            return res.Length;
        }
    }
}
