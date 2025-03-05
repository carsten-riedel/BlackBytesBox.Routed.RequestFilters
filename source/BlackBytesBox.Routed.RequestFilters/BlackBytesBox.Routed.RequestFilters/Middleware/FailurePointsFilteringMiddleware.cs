using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
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
        public FailurePointsFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<FailurePointsFilteringMiddleware> logger,  IOptionsMonitor<FailurePointsFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {OptionsName} has been updated.", nameof(FailurePointsFilteringMiddlewareOptions));
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
            MiddlewareFailurePointService.FailureSummaryByIp currentFailurePoints =  await _middlewareFailurePointService.GetSummaryByIPAsync(requestIp);

            bool isAllowed = (currentFailurePoints.FailurePoint <= options.FailurePointsLimit);

            if (isAllowed)
            {
                _logger.LogDebug("Request from IP: '{RequestIp}' is allowed. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints.FailurePoint);
                await _nextMiddleware(context);
            }
            else
            {
                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Request from IP: '{RequestIp}' exceeded failure limit, but processing will continue as configured. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints.FailurePoint);
                    await _nextMiddleware(context);
                }
                else
                {
                    _logger.LogDebug("Request from IP: '{RequestIp}' is blocked due to exceeding the failure limit. Current failure point: {FailurePoint}.", requestIp, currentFailurePoints.FailurePoint);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                }
            }
        }
    }
}
