using System;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Extensions.HttpContextExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.HttpResponseExtensions;
using BlackBytesBox.Routed.RequestFilters.Extensions.StringExtensions;
using BlackBytesBox.Routed.RequestFilters.Middleware.Options;
using BlackBytesBox.Routed.RequestFilters.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Middleware
{
    /// <summary>
    /// Middleware that filters HTTP requests based on the request URL path against configured whitelist and blacklist rules.
    /// </summary>
    /// <remarks>
    /// This middleware examines the complete URL of incoming HTTP requests and validates it against allowed (whitelist) 
    /// and disallowed (blacklist) patterns defined in the middleware options. If the request does not meet the criteria, 
    /// it either logs the issue and updates the failure point or returns an error response based on the configuration.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // In Startup.cs or Program.cs, add the middleware to the pipeline:
    /// app.UseMiddleware&lt;RequestUrlFilteringMiddleware&gt;();
    /// </code>
    /// </example>
    public class RequestUrlFilteringMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<RequestUrlFilteringMiddleware> _logger;
        private readonly IOptionsMonitor<RequestUrlFilteringMiddlewareOptions> _optionsMonitor;
        private readonly MiddlewareFailurePointService _middlewareFailurePointService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestUrlFilteringMiddleware"/> class.
        /// </summary>
        /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance for recording middleware operations.</param>
        /// <param name="optionsMonitor">The options monitor for retrieving the middleware configuration.</param>
        /// <param name="middlewareFailurePointService">The service used for tracking failure points when filtering fails.</param>
        /// <remarks>
        /// Sets up the middleware and registers a callback for configuration changes.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Example usage in a dependency injection container:
        /// services.AddTransient&lt;RequestUrlFilteringMiddleware&gt;();
        /// </code>
        /// </example>
        public RequestUrlFilteringMiddleware(RequestDelegate nextMiddleware, ILogger<RequestUrlFilteringMiddleware> logger, IOptionsMonitor<RequestUrlFilteringMiddlewareOptions> optionsMonitor, MiddlewareFailurePointService middlewareFailurePointService)
        {
            _nextMiddleware = nextMiddleware;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _middlewareFailurePointService = middlewareFailurePointService;

            _optionsMonitor.OnChange(updatedOptions =>
            {
                _logger.LogDebug("Configuration for {MiddlewareName} has been updated.", nameof(RequestUrlFilteringMiddleware));
            });
        }

        /// <summary>
        /// Processes the HTTP request by validating its URL path against allowed patterns and either continuing the pipeline 
        /// or responding with an error.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Retrieves the current middleware options, builds the full request URI, validates the URL path against whitelist and blacklist rules,
        /// and logs relevant events. Depending on the configuration, it either forwards the request to the next middleware or returns an error response.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // In a middleware pipeline:
        /// await requestUrlFilteringMiddleware.InvokeAsync(httpContext);
        /// </code>
        /// </example>
        public async Task InvokeAsync(HttpContext context)
        {
            var options = _optionsMonitor.CurrentValue;
            var uriPath = GetFullRequestUri(context)?.LocalPath;
            uriPath ??= String.Empty;

            var isAllowed = uriPath.ValidateWhitelistBlacklist(options.Whitelist, options.Blacklist);
            if (uriPath == string.Empty)
            {
                isAllowed = false;
            }

            if (isAllowed)
            {
                _logger.LogDebug("Allowed: requestUrl '{Request}' - continuing.", uriPath);
                await _nextMiddleware(context);
                return;
            }
            else
            {
                await _middlewareFailurePointService.AddOrUpdateFailurePointAsync(
                   context.GetItem<string>("remoteIpAddressStr"),
                    nameof(RequestUrlFilteringMiddleware),
                    options.DisallowedFailureRating,
                    DateTime.UtcNow);

                if (options.ContinueOnDisallowed)
                {
                    _logger.LogDebug("Disallowed: requestUrl '{Request}' - continuing.", uriPath);
                    await _nextMiddleware(context);
                    return;
                }
                else
                {
                    _logger.LogDebug("Disallowed: requestUrl '{Request}' - aborting.", uriPath);
                    await context.Response.WriteDefaultStatusCodeAnswer(options.DisallowedStatusCode);
                    return;
                }
            }
        }

        /// <summary>
        /// Builds the complete URI from the current HTTP request.
        /// </summary>
        /// <param name="context">The HTTP context containing the request.</param>
        /// <returns>
        /// The full URI of the request, or null if the URI is invalid or cannot be constructed.
        /// </returns>
        /// <remarks>
        /// Constructs the URI by combining the scheme, host, port, path, and query string of the request.
        /// If the scheme or host is missing, or an exception occurs during URI construction, the method returns null and logs the issue.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Usage example:
        /// Uri? fullUri = requestUrlFilteringMiddleware.GetFullRequestUri(httpContext);
        /// </code>
        /// </example>
        public Uri? GetFullRequestUri(HttpContext context)
        {
            try
            {
                var request = context.Request;

                // Validation checks
                if (string.IsNullOrEmpty(request.Scheme) || string.IsNullOrEmpty(request.Host.Host))
                {
                    return null;
                }

                // Build the full URI
                var uriBuilder = new UriBuilder
                {
                    Scheme = request.Scheme,
                    Host = request.Host.Host,
                    Port = request.Host.Port ?? -1, // Keep default port handling
                    Path = request.PathBase.Add(request.Path).ToString(),
                    Query = request.QueryString.ToString()
                };

                return uriBuilder.Uri;
            }
            catch (Exception ex)
            {
                // Log the exception details
                _logger.LogDebug(ex, "Failed to build full request URI. {DisplayUrl}", context.Request.GetDisplayUrl());
                return null; // or handle as appropriate
            }
        }
    }
}
