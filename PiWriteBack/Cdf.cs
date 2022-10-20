using Cognite.Extractor.Common;
using Cognite.Extractor.Utils;
using CogniteSdk;
using Com.Cognite.V1.Timeseries.Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack
{
    /// <summary>
    /// Class that handles the queries to CDF. Finds the configured time
    /// series and extract data points
    /// </summary>
    public class Cdf
    {
        private readonly CogniteDestination _cdf;
        private readonly TimeSeriesConfig _tsConfig;
        private readonly WriteBackState _state;

        public Cdf(
            FullConfig config, 
            CogniteDestination cogniteDestination,
            WriteBackState state)
        {
            _cdf = cogniteDestination;
            _tsConfig = config.TimeSeries;
            _state = state;
        }

        public async Task Init(CancellationToken token)
        {
            // Test configuration (inspect token)
            await _cdf.TestCogniteConfig(token);

            // Read CDF time series Ids from config file and
            // fetch them from CDF, if exists.
            var ts = _cdf.CogniteClient.TimeSeries;
            var ids = _tsConfig.ExternalIds.Select(t => new Identity(t)).ToList();
            var timeSeries = await ts.RetrieveAsync(ids, true, token);

            // Initialize extraction state
            await _state.InitTimeSeries(timeSeries, token);
        }

        public async Task<Dictionary<string, NumericDatapoints>> GetDataPoints(
            CancellationToken token)
        {
            var results = new Dictionary<string, NumericDatapoints>();
            var fullList = _state.State.Values.Select(s =>
            {
                var item = new DataPointsQueryItem
                {
                    ExternalId = s.Id,

                };
                if (!s.DestinationExtractedRange.IsEmpty)
                {
                    item.Start = (s.DestinationExtractedRange.Last.ToUnixTimeMilliseconds() + 1).ToString();
                }
                return item;
            }).ToList();

            foreach (IEnumerable<DataPointsQueryItem> batch in fullList.ChunkBy(100))
            {
                var query = new DataPointsQuery
                {
                    Items = batch,
                    Limit = _tsConfig.MaxDataPointsPerQuery,
                    IgnoreUnknownIds = true
                };
                var response = await _cdf.CogniteClient.DataPoints.ListAsync(query, token); // this has a limit of 100
                var filter = response.Items
                .Where(i => i.NumericDatapoints != null && i.NumericDatapoints.Datapoints != null && i.NumericDatapoints.Datapoints.Any())
                .ToDictionary(r => r.ExternalId, r => r.NumericDatapoints);

                foreach (var rItem in filter)
                {
                    results.Add(rItem.Key, rItem.Value);
                }
            }

            return results;
        }
    }
}
