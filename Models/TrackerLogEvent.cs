using System;

namespace PlayArchive.Models
{
    public class TrackerLogEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string GameTitle { get; set; } = string.Empty;
        public LogEventType Type { get; set; }
        public string Message { get; set; } = string.Empty;

        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");
    }

    public enum LogEventType
    {
        GameStarted,
        GameStopped,
        TrackingStatus,
        Info,
        Warning
    }
}
