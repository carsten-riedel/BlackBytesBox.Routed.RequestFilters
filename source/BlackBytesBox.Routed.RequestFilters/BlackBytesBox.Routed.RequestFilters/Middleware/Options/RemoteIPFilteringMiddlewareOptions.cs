using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class RemoteIPFilteringMiddlewareOptions
    {
        public string[]? Whitelist { get; set; } = null;
        public string[]? Blacklist { get; set; } = new[] { "8.8.8.8" };
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public int DisallowedFailureRating { get; set; } = 1;
        public bool ContinueOnDisallowed { get; set; } = true;
    }
}
