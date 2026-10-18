using System;
using System.Collections.Generic;

namespace PlayArchive.Models
{
    public class GameItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty; // e.g. "Cyberpunk2077.exe" or "Cyberpunk2077"
        public string? ExecutablePath { get; set; }
        public string? SteamAppId { get; set; }
        public string? CoverImage { get; set; } // URL, file path, or icon identifier
        public string Category { get; set; } = "General"; // RPG, Action, Indie, Strategy, FPS, etc.
        public double TotalPlayTimeMinutes { get; set; } = 0;
        public bool IsTrackingEnabled { get; set; } = true;
        public string? Notes { get; set; }
        public DateTime? LastPlayed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // EF Core Navigation Property
        public List<GameSession> Sessions { get; set; } = new();

        public string FormattedTotalPlayTime
        {
            get
            {
                TimeSpan ts = TimeSpan.FromMinutes(TotalPlayTimeMinutes);
                if (ts.TotalHours >= 1)
                {
                    return $"{ts.TotalHours:F1} hrs";
                }
                return $"{ts.TotalMinutes:F0} mins";
            }
        }
    }
}
