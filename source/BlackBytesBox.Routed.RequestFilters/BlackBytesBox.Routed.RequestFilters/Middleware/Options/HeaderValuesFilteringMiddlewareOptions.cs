using System.Collections.Generic;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    /// <summary>
    /// Represents the configuration options for header values filtering middleware.
    /// </summary>
    public class HeaderValuesFilteringMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets the dictionary of header filtering rules.
        /// Each key represents a header name, and the value contains whitelist and blacklist patterns.
        /// </summary>
        public Dictionary<string, HeaderFilterRule> Headers { get; set; } = new Dictionary<string, HeaderFilterRule>();

        /// <summary>
        /// Gets or sets the HTTP status code to return when a header value is disallowed.
        /// </summary>
        public int DisallowedStatusCode { get; set; } = 400;

        /// <summary>
        /// Gets or sets the failure rating threshold to trigger the disallowed response.
        /// </summary>
        public int DisallowedFailureRating { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether the request processing should continue even if a disallowed header value is detected.
        /// </summary>
        public bool ContinueOnDisallowed { get; set; } = true;
    }

    /// <summary>
    /// Represents filtering rules for a specific header.
    /// </summary>
    public class HeaderFilterRule
    {
        /// <summary>
        /// Gets or sets the list of whitelist patterns for the header.
        /// If a header value matches any of these patterns, it is allowed.
        /// </summary>
        public string[]? Whitelist { get; set; }

        /// <summary>
        /// Gets or sets the list of blacklist patterns for the header.
        /// If a header value matches any of these patterns, it is disallowed.
        /// </summary>
        public string[]? Blacklist { get; set; }
    }
}

