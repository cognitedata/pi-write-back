using Cognite.Extractor.StateStorage;
using CogniteSdk;
using OSIsoft.AF.PI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PiWriteBack
{
    /// <summary>
    /// Class to manage extraction state. Keeps a dictionary
    /// of time series with their extraction rage. The state
    /// is stored in a local file
    /// </summary>
    public class WriteBackState
    {
        private readonly IExtractionStateStore _store;
        public Dictionary<string, TagExtractionState> State { get; }

        public WriteBackState(IExtractionStateStore store)
        {
            _store = store;
            State = new Dictionary<string, TagExtractionState>();
        }

        internal async Task InitTimeSeries(IEnumerable<TimeSeries> timeSeries, CancellationToken token)
        {
            foreach (TimeSeries ts in timeSeries)
            {
                var tsState = new TagExtractionState(ts);
                State.Add(ts.ExternalId, tsState);
            }
            await _store.RestoreExtractionState(
                State,
                "extraction_state",
                true,
                token);
        }

        internal async Task StoreState(CancellationToken token)
        {
            await _store.StoreExtractionState(
                State.Values,
                "extraction_state",
                token);
        }
    }
    
    /// <summary>
    /// State of individual tags. Keeps a reference to the CDF
    /// time series and the PI tag. As well as the extration range
    /// </summary>
    public class TagExtractionState : BaseExtractionState
    {
        public TimeSeries TimeSeries { get; }
        public PIPoint PiPoint { get; internal set; }

        public TagExtractionState(TimeSeries ts) : base(ts.ExternalId)
        {
            TimeSeries = ts;
        }
    }
}
