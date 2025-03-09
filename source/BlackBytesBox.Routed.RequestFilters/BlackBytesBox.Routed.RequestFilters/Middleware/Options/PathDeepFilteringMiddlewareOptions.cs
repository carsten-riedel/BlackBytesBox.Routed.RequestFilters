using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class PathDeepFilteringMiddlewareOptions
    {
        public int PathDeepLimit { get; set; }
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
        public int DisallowedFailureRating { get; set; } = 1;
        public bool ContinueOnDisallowed { get; set; } = true;
    }
}
