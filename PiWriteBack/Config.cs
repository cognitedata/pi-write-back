using Cognite.Extractor.Utils;
using System.Collections.Generic;

namespace Cognite.PiWriteBack
{
    public class FullConfig : BaseConfig
    {
        public PiConfig Pi { get; set; }
        public TimeSeriesConfig TimeSeries { get; set; }
        public int PollingInterval { get; set; } = 60_000;
        public override void GenerateDefaults()
        {
            base.GenerateDefaults();
            if (Pi == null) Pi = new PiConfig();
            if (TimeSeries == null) TimeSeries = new TimeSeriesConfig();
        }
    }

    public class PiConfig
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool NativeAuthentication { get; set; }
        /// <summary>
        /// If this property is set to <c>true</c> and <see cref="Host"/> is a PI Collective or a Collective member,
        /// the extractor will attempt to connect to the collective, and the member will be selected according to
        /// the member priority. Else the extractor will connect to the collective, and switch to the member with the same
        /// name as <see cref="Host"/>, regardless of priority
        /// </summary>
        public bool UseMemberPriority { get; set; }
    }

    public class TimeSeriesConfig
    {
        public List<string> ExternalIds { get; set; }
        public int MaxDataPointsPerQuery { get; set; } = 1000;
    }

}
