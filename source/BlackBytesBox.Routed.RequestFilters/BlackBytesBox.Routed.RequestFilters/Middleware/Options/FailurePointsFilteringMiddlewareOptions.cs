namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class FailurePointsFilteringMiddlewareOptions
    {
        public int FailurePointsLimit { get; set; }
        public bool ContinueOnDisallowed { get; set; }
        public int DisallowedStatusCode { get; set; }
    }
}
