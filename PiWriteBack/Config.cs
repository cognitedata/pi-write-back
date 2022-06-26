using Cognite.Extractor.Utils;
using System.Collections.Generic;

namespace PiWriteBack
{
    public class Config : BaseConfig
    {
        public PiConfig Pi { get; set; }
        public TimeSeriesConfig TimeSeries { get; set; }
        public int PollingInterval { get; set; } = 60_000;
    }

    public class PiConfig
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class TimeSeriesConfig
    {
        public List<string> ExternalIds { get; set; }
        public int MaxDataPointsPerQuery { get; set; } = 1000;
    }

}
