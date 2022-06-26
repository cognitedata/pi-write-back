using Cognite.Extractor.Common;
using Com.Cognite.V1.Timeseries.Proto;
using Microsoft.Extensions.Logging;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.PI;
using OSIsoft.AF.Time;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PiWriteBack
{
    public class PiServer
    {
        private readonly PiConfig _config;
        private readonly ILogger<PiServer> _logger;
        private readonly WriteBackState _state;
        private PIServer _server;

        /// <summary>
        /// Class that handles the connection to the PI server. Includes functionality
        /// to create PI Tags and insert data points.
        /// </summary>
        public PiServer(
            PiConfig config, 
            ILogger<PiServer> logger,
            WriteBackState state)
        {
            _config = config;
            _logger = logger;
            _state = state;
        }

        public async Task Init()
        {
            NetworkCredential credential = new NetworkCredential(_config.Username, _config.Password ?? string.Empty);
            PIServer server = await Task.Run(() => PIServer.FindPIServer(_config.Host));
            await Task.Run(() => server.Connect(credential, AFConnectionPreference.Any, PIAuthenticationMode.WindowsAuthentication));
            _logger.LogInformation("Connected to server {Server}", server.Name);
            _server = server;
        }

        public void CreateTags()
        {
            foreach (var state in _state.State.Values)
            {
                var timeseries = state.TimeSeries;
                PIPoint point;
                try
                {
                    point = PIPoint.FindPIPoint(_server, timeseries.Name);
                }
                catch (PIPointInvalidException)
                {
                    // Create PI tag, if does not yet exist
                    var attrs = new Dictionary<string, object>
                    {
                        [PICommonPointAttributes.PointType] = PIPointType.Float64, // double precision
                        [PICommonPointAttributes.Descriptor] = timeseries.Description,
                        [PICommonPointAttributes.PointSource] = timeseries.ExternalId
                    };
                    point = _server.CreatePIPoint(timeseries.Name, attrs);
                }

                state.PiPoint = point;
            }
        }

        public async Task InsertDataPoints(
            Dictionary<string, NumericDatapoints> dataPoints,
            CancellationToken token)
        {
            foreach(var entry in dataPoints)
            {
                if (entry.Value == null || !entry.Value.Datapoints.Any())
                {
                    continue;
                }

                if (!_state.State.TryGetValue(entry.Key, out var tagState))
                {
                    _logger.LogWarning("Extraction state with key {Key} not found", entry.Key);
                    continue;
                }

                // Convert CDF data points to AF values
                var afValues = entry.Value.Datapoints.Select(dp => new AFValue
                {
                    Timestamp = new AFTime(CogniteTime.FromUnixTimeMilliseconds(dp.Timestamp)),
                    Value = dp.Value
                }).ToList();

                // Insert or replace the points into the PI Point (Tag)
                var result = await tagState.PiPoint.UpdateValuesAsync(afValues, OSIsoft.AF.Data.AFUpdateOption.Replace, token);
                if (result != null && result.HasErrors)
                {
                    foreach (var error in result.Errors)
                    {
                        // TODO: should hande retryable errors
                        _logger.LogError("Could not write data point: {Error}", error.Value.Message);
                    }
                }

                // Update the extraction rage
                var (Min, Max) = entry.Value.Datapoints.MinMax(dp => dp.Timestamp);
                tagState.UpdateDestinationRange(
                    CogniteTime.FromUnixTimeMilliseconds(Min), 
                    CogniteTime.FromUnixTimeMilliseconds(Max));
                _logger.LogDebug("Tag {Name} updated with {Num} datapoints in the range ({Min} - {Max})",
                    tagState.PiPoint.Name,
                    afValues.Count,
                    CogniteTime.FromUnixTimeMilliseconds(Min).ToISOString(),
                    CogniteTime.FromUnixTimeMilliseconds(Max).ToISOString());
            }
        }
    }
}
