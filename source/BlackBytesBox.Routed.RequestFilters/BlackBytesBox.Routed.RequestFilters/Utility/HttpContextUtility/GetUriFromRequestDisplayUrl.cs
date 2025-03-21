using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace BlackBytesBox.Routed.RequestFilters.Utility.HttpContextUtility
{
    public static partial class HttpContextUtility
    {
        /// <summary>
        /// Retrieves the full request URI using the display URL.
        /// </summary>
        /// <param name="context">The HTTP context containing the request.</param>
        /// <param name="logger">An optional logger for debug logging.</param>
        /// <returns>
        /// The full URI of the request if successfully constructed; otherwise, <c>null</c>.
        /// </returns>
        public static Uri? GetUriFromRequestDisplayUrl(HttpContext context, ILogger? logger = null)
        {
            if (context is null)
            {
                logger?.LogDebug("HttpContext is null.");
                return null;
            }

            string displayUrl = context.Request.GetDisplayUrl();

            if (string.IsNullOrWhiteSpace(displayUrl))
            {
                logger?.LogDebug("Display URL is empty.");
                return null;
            }

            try
            {
                return new Uri(displayUrl);
            }
            catch (Exception)
            {
                logger?.LogDebug("Failed to parse display URL: {DisplayUrl}", displayUrl);
                return null;
            }
        }

    }
}