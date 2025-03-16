using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class HostNameFilteringMiddlewareOptions
    {
        public string[]? Whitelist { get; set; }
        public string[]? Blacklist { get; set; }
        public bool? CaseSensitive { get; set; } = false;
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public int DisallowedFailureRating { get; set; } = 1;
        public bool ContinueOnDisallowed { get; set; } = true;
    }
}
