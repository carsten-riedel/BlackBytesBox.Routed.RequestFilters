using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class HttpProtocolFilteringMiddlewareOptions
    {
        public string[]? Whitelist { get; set; } = new[] { "HTTP/1.1", "HTTP/2", "HTTP/2.0", "HTTP/3", "HTTP/3.0" };
        public string[]? Blacklist { get; set; } = new[] { "", "HTTP/1.0", "HTTP/1.?" };
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public int DisallowedFailureRating { get; set; } = 1;
        public bool ContinueOnDisallowed { get; set; } = true;
    }
}