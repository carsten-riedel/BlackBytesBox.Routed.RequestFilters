using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BlackBytesBox.Routed.RequestFilters.Services.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlackBytesBox.Routed.RequestFilters.Services
{
    public class MiddlewareFailurePointService
    {
        public class FailurePointItem
        {
            public int FailurePoint { get; set; }
            public string RequestIp { get; set; }
            public string FailureSource { get; set; }
            public DateTime RequestedTime { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="FailurePointItem"/> class.
            /// </summary>
            /// <param name="requestIp">The request IP.</param>
            /// <param name="failureSource">The source responsible for the failure.</param>
            /// <param name="failurePoint">The number of points assigned.</param>
            /// <param name="requestedTime">The timestamp when the failure occurred.</param>
            public FailurePointItem(string requestIp, string failureSource, int failurePoint, DateTime requestedTime)
            {
                RequestIp = requestIp;
                FailureSource = failureSource;
                RequestedTime = requestedTime;
                FailurePoint = failurePoint;
            }
        }

        public class FailureSummaryBySource
        {
            public int FailurePoint { get; set; }
            public string FailureSource { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="FailureSummaryBySource"/> class.
            /// </summary>
            /// <param name="failureSource">The source responsible for the failure.</param>
            /// <param name="failurePoint">The number of points assigned.</param>
            public FailureSummaryBySource(string failureSource, int failurePoint)
            {
                FailureSource = failureSource;
                FailurePoint = failurePoint;
            }
        }

        public class FailureSummaryByIp
        {
            public int FailurePoint { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="FailureSummaryByIp"/> class.
            /// </summary>
            /// <param name="failurePoint">The number of points assigned.</param>
            public FailureSummaryByIp(int failurePoint)
            {
                FailurePoint = failurePoint;
            }
        }

        // Thread‑safe collections for failure summaries.
        private readonly PerKeyLockableList<FailureSummaryBySource> _summaryBySource = new();
        private readonly PerKeyLockable<FailureSummaryByIp> _summaryByIp = new();

        private readonly ILogger<MiddlewareFailurePointService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MiddlewareFailurePointService"/> class.
        /// </summary>
        /// <param name="options">The options monitor for service configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="hostApplicationLifetime">The host application lifetime.</param>
        public MiddlewareFailurePointService(IOptionsMonitor<MiddlewareFailurePointServiceOptions> options, ILogger<MiddlewareFailurePointService> logger, IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Adds or updates failure points for a given request IP and source.
        /// </summary>
        /// <param name="requestIp">The request IP.</param>
        /// <param name="failureSource">The source responsible for the failure (e.g. using <c>nameof</c>).</param>
        /// <param name="failurePoint">The number of points to add.</param>
        /// <param name="requestedTime">The timestamp when the failure occurred.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddOrUpdateFailurePointAsync(string requestIp, string failureSource, int failurePoint, DateTime requestedTime)
        {
            _logger.LogInformation("AddOrUpdateFailurePointAsync started for RequestIp: {RequestIp}, Source: {Source}, FailurePoint: {FailurePoint}, RequestedTime: {RequestedTime}",requestIp, failureSource, failurePoint, requestedTime);

            // Update the failure summary by source for the given requestIp key.
            await _summaryBySource.UpdateAsync(requestIp, list =>
            {
                var item = list.FirstOrDefault(e => e.FailureSource == failureSource);
                if (item == null)
                {
                    list.Add(new FailureSummaryBySource(failureSource, failurePoint));
                    _logger.LogDebug("Created new failure summary for Source {Source} on RequestIp {RequestIp} with initial FailurePoint {FailurePoint}.",failureSource, requestIp, failurePoint);
                }
                else
                {
                    item.FailurePoint += failurePoint;
                    _logger.LogDebug("Updated failure summary for Source {Source} on RequestIp {RequestIp}. New FailurePoint: {FailurePoint}.",failureSource, requestIp, item.FailurePoint);
                }
            });

            // Update the failure summary by IP for the given requestIp key.
            await _summaryByIp.UpdateAsync(requestIp, current =>
            {
                current ??= new FailureSummaryByIp(0);
                current.FailurePoint += failurePoint;
                _logger.LogDebug("Updated failure summary for RequestIp {RequestIp}. New FailurePoint: {FailurePoint}.",requestIp, current.FailurePoint);
                return current;
            });
        }
    }
}
