﻿using System;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests by validating the request's failure points
    /// against configured limits. If the failure count for the client IP exceeds the allowed limit,
    /// the middleware either blocks the request or continues processing based on configuration.
    /// </summary>
    public class FailurePointsFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<FailurePointsFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<FailurePointsFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FailurePointsFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance used for logging middleware events.</param>
        /// <param name="optionsMonitor">The monitor for retrieving configuration settings for the middleware.</param>
        /// <param name="middlewareFailurePointService">
        /// The service used for tracking and recording failure points when requests do not meet the configured criteria.
        /// </param>
        public FailurePointsFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<FailurePointsFilteringMiddleware> logger,  IOptionsMonitor<FailurePointsFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService, IHostApplicationLifetime hostApplicationLifetime)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(FailurePointsFilteringMiddlewareOptions));
            });


            // Register a callback to load persisted data when the host starts.
            hostApplicationLifetime.ApplicationStarted.Register(() =>
            {
                try
                {
                    var loadTask = _middlewareFailurePointService.LoadDataFromJsonAsync(_optionsMonitor.CurrentValue.DumpFilePath);
                    bool loadedInTime = loadTask.Wait(TimeSpan.FromSeconds(10));
                    if (!loadedInTime)
                    {
                        _logger.LogWarning("Loading persisted data did not complete within the 10-second timeout. Continuing without full data restoration.");
                    }
                    else
                    {
                        _logger.LogInformation("Successfully loaded persisted data during application startup.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading persisted data during application startup.");
                }
            });

            // Register an asynchronous callback when the host starts.
            // Register a callback to dump all data when the application is stopping.
            hostApplicationLifetime.ApplicationStopping.Register(() =>
            {
                try
                {
                    // Start the dump operation and wait synchronously for up to 10 seconds.
                    var dumpTask = _middlewareFailurePointService.DumpDataToJsonAsync(_optionsMonitor.CurrentValue.DumpFilePath);
                    bool completedInTime = dumpTask.Wait(TimeSpan.FromSeconds(10));
                    if (!completedInTime)
                    {
                        _logger.LogWarning("Dumping data did not complete within the 10-second timeout. Proceeding with shutdown.");
                    }
                    else
                    {
                        _logger.LogInformation("Successfully dumped all data during application shutdown.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dumping data during application shutdown.");
                }
            });
        }

        /// <summary>
        /// Processes the HTTP request by evaluating the current failure points for the client's IP.
        /// If the failure count is within the allowed limit, the request is forwarded; otherwise, the request
        /// is either blocked or allowed to continue based on the configuration.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Retrieve current configuration options.
            var options = _optionsMonitor.CurrentValue;

            // Retrieve the client's IP address for logging and validation.
            string? requestIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(requestIp))
            {
                _logger.LogError("Request rejected: Missing valid IP address.");
                await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                return;
            }

            // Retrieve the failure summary for the client's IP.
            //var currentFailurePointsx = await _middlewareFailurePointService.GetSummaryBySourceAsync(requestIp);
            MiddlewareFailurePointService.FailureSummaryByIp currentFailurePoints =  await _middlewareFailurePointService.GetSummaryByIPAsync(requestIp);

            bool isAllowed = false;
            if (currentFailurePoints == null || (currentFailurePoints.FailurePoint <= options.FailurePointsLimit))
            {
                isAllowed = true;
            }

            if (isAllowed)
            {
                _logger.LogDebug("Request from IP: '{RequestIp}' is allowed. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints?.FailurePoint ?? 0);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Request from IP: '{RequestIp}' exceeded failure limit, but processing will continue as configured. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints?.FailurePoint ?? 0);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Request from IP: '{RequestIp}' is blocked due to exceeding the failure limit. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints?.FailurePoint ?? 0);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }
    }
}
