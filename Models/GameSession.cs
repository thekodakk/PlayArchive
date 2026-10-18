using System;

namespace PlayArchive.Models
{
    public class GameSession
    {
        public int Id { get; set; }
        public int GameItemId { get; set; }
        public GameItem? GameItem { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double DurationMinutes { get; set; } = 0;
        public bool IsActive { get; set; } = false;
        public string? Notes { get; set; }

        public string FormattedDuration
        {
            get
            {
                if (IsActive)
                {
                    TimeSpan live = DateTime.Now - StartTime;
                    return $"{live.Hours:D2}:{live.Minutes:D2}:{live.Seconds:D2} (Live)";
                }

                TimeSpan ts = TimeSpan.FromMinutes(DurationMinutes);
                if (ts.TotalHours >= 1)
                {
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
                }
                return $"{ts.Minutes}m {ts.Seconds}s";
            }
        }
    }
}
