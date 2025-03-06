using System;

using Microsoft.Extensions.Configuration;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class FailurePointsFilteringMiddlewareOptions
    {
        public string DumpFilePath { get; set; } = System.IO.Path.Combine(AppContext.BaseDirectory, "FailurePointsFilteringMiddleware.json");
        public int FailurePointsLimit { get; set; }
        public bool ContinueOnDisallowed { get; set; }
        public int DisallowedStatusCode { get; set; }
    }
}
