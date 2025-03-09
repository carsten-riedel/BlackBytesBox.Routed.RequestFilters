using System;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BlackBytesBox.Routed.RequestFilters.Middleware.Options
{
    public class FailurePointsFilteringMiddlewareOptions
    {
        public string DumpFilePath { get; set; } = System.IO.Path.Combine(AppContext.BaseDirectory, "FailurePointsFilteringMiddleware.json");
        public int FailurePointsLimit { get; set; } = 0;
        public bool ContinueOnDisallowed { get; set; } = false;
        public int DisallowedStatusCode { get; set; } = StatusCodes.Status400BadRequest;
    }
}
