namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class PathDeepFilteringMiddlewareOptions
    {
        public int PathDeepLimit { get; set; }
        public int DisallowedStatusCode { get; set; }
        public int DisallowedFailureRating { get; set; }
        public bool ContinueOnDisallowed { get; set; }
    }
}
