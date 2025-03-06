using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using BlackBytesBox.Routed.RequestFilters.Services;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on their protocol against configured allowed protocols.
    /// </summary>
    public class IPRequiredFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<IPRequiredFilteringMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpProtocolFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="optionsMonitor">The options monitor for the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points.</param>
        public IPRequiredFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<IPRequiredFilteringMiddleware> logger)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
        }

        /// <summary>
        /// Processes the HTTP request by validating its protocol and either forwarding the request
        /// or responding with an error based on the configured rules.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            System.Net.IPAddress? remoteIpAddress = context.Connection.RemoteIpAddress;
            if (remoteIpAddress != null)
            {
                context.Items[nameof(remoteIpAddress)] = remoteIpAddress?.ToString();
                await _nextMiddleware(context);
                return;
            }
            else
            {
                _logger.LogError("Request rejected: Missing valid IP address.");
                await context.Response.WriteDefaultStatusCodeAnswer(StatusCodes.Status400BadRequest);
                return;
            }
        }
    }
}