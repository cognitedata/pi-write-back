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

namespace Cognite.PiWriteBack
{
    public class PiServer
    {
        private readonly PiConfig _config;
        private readonly ILogger<PiServer> _logger;
        private readonly WriteBackState _state;
        private PIServer _server;
        // private AFDatabase _database;

        /// <summary>
        /// Class that handles the connection to the PI server. Includes functionality
        /// to create PI Tags and insert data points.
        /// </summary>
        public PiServer(
            FullConfig config, 
            ILogger<PiServer> logger,
            WriteBackState state)
        {
            _config = config.Pi;
            _logger = logger;
            _state = state;
        }

        public async Task Init()
        {
            // NetworkCredential credential = new NetworkCredential(_config.Username, _config.Password ?? string.Empty);
            // PIServer server = await Task.Run(() => PIServer.FindPIServer(_config.Host));
            // await Task.Run(() => server.Connect(credential, AFConnectionPreference.Any, PIAuthenticationMode.WindowsAuthentication));
            // _logger.LogInformation("Connected to server {Server}", server.Name);
            // _server = server;

            var username = _config.Username.TrimToNull();
            var mode = _config.NativeAuthentication ? PIAuthenticationMode.PIUserAuthentication : PIAuthenticationMode.WindowsAuthentication;
            NetworkCredential credential = null;
            if (username != null)
            {
                credential = new NetworkCredential(_config.Username, _config.Password ?? string.Empty);
            }

            PIServer server = await Task.Run(() => PIServer.FindPIServer(_config.Host));
            if (server == null)
            {
                _logger.LogWarning("PI server not found: {PiServerName}", _config.Host);
            }
            else
            {
                PICollectiveMember collectiveMember = null;
                if (_config.UseMemberPriority && server.Collective != null && server.Name != _config.Host)
                {
                    // If the host is a collective member, find the member and later
                    // switch the collective connection to that member
                    PIConnectionInfo.DefaultPreference = AFConnectionPreference.Any;
                    foreach (var member in server.Collective.Members)
                    {
                        if (member.Name == _config.Host)
                        {
                            collectiveMember = member;
                            break;
                        }
                    }
                }
                if (credential == null)
                {
                    await Task.Run(() => server.Connect());
                }
                else
                {
                    _logger.LogWarning("Using mode: {mode}", mode);
                    await Task.Run(() => server.Connect(credential, AFConnectionPreference.Any, mode));
                }
                if (!server.ConnectionInfo.IsConnected)
                {
                    _logger.LogWarning("PI Server is not connected");
                }
                if (collectiveMember != null && !collectiveMember.IsConnected)
                {
                    server.Collective.SwitchMember(credential, mode, collectiveMember);
                }
                if (collectiveMember != null && !collectiveMember.IsConnected)
                {
                    _logger.LogWarning("PI Collective member is not connected");
                }
                _logger.LogInformation("Connected to PI {PiServerName}. Version: {PiServerVersion}", GetConnectedServerString(server), server.ServerVersion);

                _server = server;
            }
        }

        public string GetConnectedServerString(PIServer server)
        {
            if (!server.ConnectionInfo.IsConnected)
            {
                return $"Server {server.Name} (Not Connected)";
            }
            if (server.Collective != null)
            {
                var current = server.Collective.CurrentMember;
                return $"Collective {server.Collective}, member {current.Name} ({current.ServerRole})";
            }
            return $"Server {server.Name}";
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
                    _logger.LogInformation("Found timeseries as PI point: {0}", timeseries.Name);
                }
                catch (PIPointInvalidException)
                {
                    _logger.LogInformation("Creating timeseries: {0}", timeseries.Name);
                    // Create PI tag, if does not yet exist
                    var attrs = new Dictionary<string, object>
                    {
                        [PICommonPointAttributes.PointType] = PIPointType.Float64, // double precision
                        [PICommonPointAttributes.Descriptor] = timeseries.Description == null ? "" : timeseries.Description,
                        [PICommonPointAttributes.ExtendedDescriptor] = timeseries.ExternalId,
                        [PICommonPointAttributes.PointSource] = "Cognite"
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

                // Update the extraction range
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
