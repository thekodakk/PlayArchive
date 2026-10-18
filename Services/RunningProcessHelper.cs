using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PlayArchive.Models;

namespace PlayArchive.Services
{
    public static class RunningProcessHelper
    {
        private static readonly HashSet<string> SystemNoise = new(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "csrss", "smss", "wininit", "services", "lsass", "conhost",
            "fontdrvhost", "dwm", "taskhostw", "explorer", "sihost", "ctfmon",
            "system", "idle", "searchhost", "searchindexer", "startmenuexperiencehost",
            "shellexperiencehost", "lockapp", "runtimebroker", "applicationframehost",
            "textinputhost", "systemsettings", "winstore.app", "spoolsv", "audiodg",
            "securityhealthservice", "antigravity", "antigravity-ide", "armourycrate",
            "asus", "acpowernotification", "armourycrate.usersessionHelper", "cmd", "powershell"
        };

        public static List<RunningProcessInfo> GetRunningProcesses()
        {
            var result = new List<RunningProcessInfo>();
            var processes = Process.GetProcesses();

            foreach (var proc in processes)
            {
                try
                {
                    // Filter 1: Must have an active window handle and a non-empty window title
                    if (proc.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(proc.MainWindowTitle))
                    {
                        continue;
                    }

                    string procName = proc.ProcessName;

                    // Filter 2: Exclude system processes and known background noise
                    if (SystemNoise.Contains(procName) || procName.StartsWith("svchost", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string exeName = procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? procName : procName + ".exe";
                    string title = proc.MainWindowTitle.Trim();

                    string? filePath = null;
                    try
                    {
                        filePath = proc.MainModule?.FileName;
                    }
                    catch
                    {
                        // Inaccessible system process, skip filePath silently
                    }

                    result.Add(new RunningProcessInfo
                    {
                        ProcessId = proc.Id,
                        ProcessName = procName,
                        ExecutableName = exeName,
                        MainWindowTitle = title,
                        FilePath = filePath
                    });
                }
                catch
                {
                    // Ignore inaccessible processes
                }
            }

            // Deduplicate processes by process name (e.g. only 1 entry per game/app)
            return result
                .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.MainWindowTitle)
                .ToList();
        }
    }
}
