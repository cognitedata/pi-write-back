using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack
{
    /// <summary>
    /// Simple CDF data point extractor. Ingests CDF time series
    /// into PI Tags (PiPoints).
    /// </summary>
    public class DataPointsExtractor
    {
        private readonly Cdf _cdf;
        private readonly PiServer _pi;
        private readonly WriteBackState _state;
        private readonly FullConfig _config;

        public DataPointsExtractor(
            Cdf cdf,
            PiServer pi,
            WriteBackState state,
            FullConfig config)
        {
            _cdf = cdf;
            _pi = pi;
            _state = state;
            _config = config;
        }

        public async Task Run(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                // Retrieve datapoints from the CDF time series
                var dps = await _cdf.GetDataPoints(token);

                if (!dps.Any())
                {
                    // If there are no data points to extract, sleep for the
                    // configured duration and try again
                    await Task.Delay(_config.PollingInterval, token);
                    continue;
                }

                // Insert the datapoints in the PI Server
                await _pi.InsertDataPoints(dps, token);

                // Store the extraction state (min and max timestamps)
                await _state.StoreState(token);

                // Sleep for a sec before repeating
                await Task.Delay(1000, token);
            }
        }
    }
}
