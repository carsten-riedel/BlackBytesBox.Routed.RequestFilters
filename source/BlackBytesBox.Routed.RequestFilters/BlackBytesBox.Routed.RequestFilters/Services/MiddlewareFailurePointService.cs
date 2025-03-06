using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        public MiddlewareFailurePointService(ILogger<MiddlewareFailurePointService> logger, IHostApplicationLifetime hostApplicationLifetime)
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
        /// <summary>
        /// Adds or updates the failure point for a given request IP, both by source and overall.
        /// </summary>
        /// <param name="requestIp">The IP address from which the request originated.</param>
        /// <param name="failureSource">The source of the failure.</param>
        /// <param name="failurePoint">The number of failure points to add.</param>
        /// <param name="requestedTime">The time when the request was made.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method logs an information level message with the request details and then updates two separate failure summaries.
        /// The failure summary by source is updated with a single debug log after either creating or updating the summary.
        /// </remarks>
        public async Task AddOrUpdateFailurePointAsync(string requestIp, string failureSource, int failurePoint, DateTime requestedTime)
        {
            _logger.LogDebug("RequestIp: {RequestIp}, Source: {Source}, FailurePoint: {FailurePoint}, RequestedTime: {RequestedTime}", requestIp, failureSource, failurePoint, requestedTime);

            // Update the failure summary by source for the given requestIp key.
            await _summaryBySource.UpdateAsync(requestIp, list =>
            {
                // Find the existing summary by source.
                var item = list.FirstOrDefault(e => e.FailureSource == failureSource);
                if (item == null)
                {
                    // Create and add a new summary if it does not exist.
                    item = new FailureSummaryBySource(failureSource, failurePoint);
                    list.Add(item);
                }
                else
                {
                    // Otherwise, update the existing summary.
                    item.FailurePoint += failurePoint;
                }
                // Single logging call that covers both cases.
            });

            // Update the failure summary by IP for the given requestIp key.
            await _summaryByIp.UpdateAsync(requestIp, current =>
            {
                current ??= new FailureSummaryByIp(0);
                current.FailurePoint += failurePoint;
                return current;
            });
        }

        public async Task<IReadOnlyList<FailureSummaryBySource>> GetSummaryBySourceAsync(string requestIp)
        {
            return await _summaryBySource.GetAsync(requestIp);
        }

        public async Task<FailureSummaryByIp> GetSummaryByIPAsync(string requestIp)
        {
            return await _summaryByIp.GetAsync(requestIp);
        }

        /// <summary>
        /// Asynchronously loads persisted JSON data and updates the internal collections.
        /// </summary>
        /// <param name="filePath">The file path of the JSON dump.</param>
        public async Task LoadDataFromJsonAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("No persisted data file found at {FilePath}.", filePath);
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var dumpData = JsonSerializer.Deserialize<DumpData>(json);
                if (dumpData == null)
                {
                    _logger.LogWarning("Deserialized dump data was null from file {FilePath}.", filePath);
                    return;
                }

                // Load SummaryBySource data.
                foreach (var kvp in dumpData.SummaryBySource)
                {
                    string requestIp = kvp.Key;
                    var summaries = kvp.Value;
                    foreach (var summary in summaries)
                    {
                        await _summaryBySource.UpdateAsync(requestIp, list =>
                        {
                            // You might want to clear existing data if necessary.
                            list.Add(summary);
                        });
                    }
                }

                // Load SummaryByIp data.
                foreach (var kvp in dumpData.SummaryByIp)
                {
                    string requestIp = kvp.Key;
                    var summary = kvp.Value;
                    await _summaryByIp.SetAsync(requestIp, summary);
                }

                _logger.LogInformation("Successfully loaded persisted failure point data from {FilePath}.", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading persisted data from file {FilePath}.", filePath);
            }
        }

        // Used to map the JSON structure.
        private class DumpData
        {
            public Dictionary<string, List<FailureSummaryBySource>> SummaryBySource { get; set; }
            public Dictionary<string, FailureSummaryByIp> SummaryByIp { get; set; }
        }

        /// <summary>
        /// Asynchronously dumps all stored failure summary data to a JSON file on disk.
        /// </summary>
        /// <param name="filePath">The file path where the JSON data should be saved.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DumpDataToJsonAsync(string filePath)
        {
            var summaryBySourceSnapshot = await _summaryBySource.GetAllAsync();
            var summaryByIpSnapshot = await _summaryByIp.GetAllAsync();

            var dumpData = new
            {
                SummaryBySource = summaryBySourceSnapshot,
                SummaryByIp = summaryByIpSnapshot
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(dumpData, jsonOptions);

            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("Dumped failure point data to JSON file at {FilePath}", filePath);
        }
    }
}