using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class HttpProtocolFilteringMiddlewareOptions
    {
        public string[]? Whitelist { get; set; }
        public string[]? Blacklist { get; set; }
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public int DisallowedFailureRating { get; set; } = 1;
        public bool ContinueOnDisallowed { get; set; } = true;
    }
}
