namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class SegmentFilteringMiddlewareOptions
    {
        public string[]? Whitelist { get; set; }
        public string[]? Blacklist { get; set; }
        public int DisallowedStatusCode { get; set; }
        public int DisallowedFailureRating { get; set; }
        public bool ContinueOnDisallowed { get; set; }
    }
}
