using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace BlackBytesBox.Routed.RequestFilters.Utility.HttpContextUtility
{
    public static partial class HttpContextUtility
    {
        /// <summary>
        /// Builds the complete URI from the HTTP request components.
        /// </summary>
        /// <param name="context">The HTTP context containing the request.</param>
        /// <param name="logger">An optional logger for error logging.</param>
        /// <returns>
        /// The full URI of the request if successfully constructed; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Returns null if required components (such as scheme or host) are missing or if any error occurs during construction.
        /// </remarks>
        public static Uri? ConstructUriFromRequest(HttpContext context, ILogger? logger = null)
        {
            if (context is null)
            {
                logger?.LogError("HttpContext is null.");
                return null;
            }

            HttpRequest? request = context.Request;

            try
            {
                // Validate that both the scheme and host are provided.
                if (string.IsNullOrWhiteSpace(request.Scheme) || string.IsNullOrWhiteSpace(request.Host.Host))
                {
                    logger?.LogError("Invalid request: Missing scheme or host. Scheme: {Scheme}, Host: {Host}",
                        request.Scheme, request.Host.Host);
                    return null;
                }

                // Determine the default port based on the scheme.
                int defaultPort = -1;
                if (string.Equals(request.Scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    defaultPort = 80;
                }
                else if (string.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    defaultPort = 443;
                }
                int port = request.Host.Port ?? defaultPort;

                // Combine PathBase and Path to form the full path.
                string fullPath = request.PathBase.Add(request.Path).ToString();

                // Use the query string only if it has a value.
                string query = request.QueryString.HasValue ? request.QueryString.ToString() : string.Empty;

                // Build the full URI from the request components.
                var uriBuilder = new UriBuilder
                {
                    Scheme = request.Scheme,
                    Host = request.Host.Host,
                    Port = port,
                    Path = fullPath,
                    Query = query
                };

                return uriBuilder.Uri;
            }
            catch (Exception ex)
            {
                // Ensure request is available for logging; otherwise, use a fallback.
                string displayUrl = request?.GetDisplayUrl() ?? "Unknown";
                logger?.LogError(ex, "Failed to build full request URI for {DisplayUrl}", displayUrl);
                return null;
            }
        }
    }
}