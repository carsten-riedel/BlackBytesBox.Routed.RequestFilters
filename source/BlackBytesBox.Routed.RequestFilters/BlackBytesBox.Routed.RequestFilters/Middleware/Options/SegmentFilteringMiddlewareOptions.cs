using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class SegmentFilteringMiddlewareOptions
    {
        public string FilterPriority { get; set; } = "Blacklist";
        public string[]? Whitelist { get; set; }
        public string[]? Blacklist { get; set; }
        public bool CaseSensitive { get; set; } = true;
        public int BlacklistStatusCode { get; set; } = StatusCodes.Status403Forbidden;
        public int BlacklistFailureRating { get; set; } = 1;
        public bool BlacklistContinue { get; set; } = true;
        public int NotMatchedStatusCode { get; set; } = StatusCodes.Status403Forbidden;
        public int NotMatchedFailureRating { get; set; } = 0;
        public bool NotMatchedContinue { get; set; } = true;
        public bool NotMatchedLogWarning { get; set; } = true;
        public int UnreadableStatusCode { get; set; } = StatusCodes.Status403Forbidden;
        public int UnreadableFailureRating { get; set; } = 1;
        public bool UnreadableContinue { get; set; } = true;
    }
}
