namespace PlayArchive.Models
{
    public class RunningProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutableName { get; set; } = string.Empty;
        public string MainWindowTitle { get; set; } = string.Empty;
        public string? FilePath { get; set; }
    }
}
