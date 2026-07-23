// filename: Services/EveLogWatcher.cs
// EVE Online writes Local chat logs as UTF-16-LE with BOM.
// Header lines are indented with spaces.
// Each chat line starts with a BOM character (\uFEFF).
//
// Confirmed format (from real log files):
//   "          Listener:        ARC Tendeuse A3"
//   "\uFEFF[ 2026.03.10 01:44:35 ] EVE System > Channel changed to Local : J220215"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OverlayMVP.Services
{
    public sealed class EveLogWatcher : IDisposable
    {
        public event Action<string>? SystemChanged;
        // Fired when a mission is accepted or completed in the game journal
        // args: (characterName, missionName, eventType) where eventType = "accepted"|"completed"
        public event Action<string, string, string>? MissionEvent;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);

        // Matches: "[ 2026.03.10 01:44:35 ] EVE System > Channel changed to Local : J220215"
        // The \uFEFF BOM at line start is stripped by .Trim() before matching
        private static readonly Regex SystemRx = new(
            @"Channel changed to Local\s*:\s*(.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // EVE Gamelog mission lines (appear in Gamelogs folder)
        private static readonly Regex MissionAcceptRx = new(
            @"Mission accepted:\s*(.+?)(?:\s*<|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MissionCompleteRx = new(
            @"(?:Mission completed|Objective completed):\s*(.+?)(?:\s*<|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EVE", "logs", "Chatlogs");

        private static readonly string GameLogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EVE", "logs", "Gamelogs");

        // EVE logs are UTF-16-LE
        private static readonly Encoding EveEncoding = new UnicodeEncoding(false, true);

        private readonly Dictionary<string, string> _charToFile  = new();
        private readonly Dictionary<string, long>   _fileOffsets     = new();
        private readonly Dictionary<string, long>   _gameLogOffsets  = new();
        private string _lastGameLogFile = "";
        private readonly Dictionary<string, string> _fileSystems = new();
        private readonly System.Threading.Timer     _timer;
        private string _lastReportedSystem = "";

        public EveLogWatcher()
        {
            RebuildCharFileMap();
            _timer = new System.Threading.Timer(_ => Poll(),
                null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        }

        // ── Poll ─────────────────────────────────────────────────────────
        private void Poll()
        {
            if (!Directory.Exists(LogDir)) return;
            try
            {
                RebuildCharFileMap();
                foreach (var path in _charToFile.Values.Distinct())
                    ReadNewLines(path);

                var activeChar = GetActiveEveCharacter();
                if (activeChar is null) return;

                if (!_charToFile.TryGetValue(activeChar.ToLowerInvariant(), out var logPath)) return;
                if (!_fileSystems.TryGetValue(logPath, out var system) ||
                    string.IsNullOrEmpty(system)) return;

                if (system != _lastReportedSystem)
                {
                    _lastReportedSystem = system;
                    SystemChanged?.Invoke(system);
                }

                // Also poll Gamelogs for mission events
                PollGameLog(activeChar);
            }
            catch { }
        }

        // ── Poll Gamelogs for mission accepted/completed events ───────────
        private void PollGameLog(string characterName)
        {
            if (!Directory.Exists(GameLogDir)) return;
            try
            {
                // Find the most recent gamelog file (EVE creates one per session)
                var latestFile = Directory.GetFiles(GameLogDir, "*.txt")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                if (latestFile is null) return;

                // Reset offset if file changed
                if (latestFile != _lastGameLogFile)
                {
                    _lastGameLogFile = latestFile;
                    _gameLogOffsets[latestFile] = 0;
                }

                using var fs = new FileStream(latestFile, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);

                if (!_gameLogOffsets.TryGetValue(latestFile, out long offset))
                    offset = Math.Max(0, fs.Length - 16384);
                if (offset > fs.Length) offset = 0;
                fs.Seek(offset, SeekOrigin.Begin);

                // Gamelogs are UTF-8
                using var reader = new StreamReader(fs, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true, 4096, leaveOpen: true);

                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var clean = line.TrimStart('﻿', ' ', '	');

                    var accept = MissionAcceptRx.Match(clean);
                    if (accept.Success)
                    {
                        var missionName = accept.Groups[1].Value.Trim();
                        MissionEvent?.Invoke(characterName, missionName, "accepted");
                    }

                    var complete = MissionCompleteRx.Match(clean);
                    if (complete.Success)
                    {
                        var missionName = complete.Groups[1].Value.Trim();
                        MissionEvent?.Invoke(characterName, missionName, "completed");
                    }
                }
                _gameLogOffsets[latestFile] = fs.Position;
            }
            catch { }
        }

        // ── Read new lines ────────────────────────────────────────────────
        private void ReadNewLines(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);

                if (!_fileOffsets.TryGetValue(path, out long offset))
                    offset = Math.Max(0, fs.Length - 8192);
                if (offset > fs.Length) offset = 0;

                // UTF-16-LE: align to even byte boundary
                if (offset % 2 != 0) offset--;
                fs.Seek(offset, SeekOrigin.Begin);

                using var reader = new StreamReader(fs, EveEncoding,
                    detectEncodingFromByteOrderMarks: true, 4096, leaveOpen: true);

                string? line;
                string? latest = null;
                while ((line = reader.ReadLine()) is not null)
                {
                    // Strip BOM and whitespace that EVE prepends to each line
                    var clean = line.TrimStart('﻿', ' ', '	');
                    var m = SystemRx.Match(clean);
                    if (m.Success) latest = m.Groups[1].Value.Trim();
                }
                _fileOffsets[path] = fs.Position;

                if (latest is not null)
                    _fileSystems[path] = latest;
            }
            catch { }
        }

        // ── Build char → file map ─────────────────────────────────────────
        private void RebuildCharFileMap()
        {
            if (!Directory.Exists(LogDir)) return;
            try
            {
                var files = Directory.GetFiles(LogDir, "Local_*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .ToList();

                var seen = new HashSet<string>();
                foreach (var fi in files)
                {
                    var charName = ReadListenerHeader(fi.FullName);
                    if (charName is null) continue;
                    var key = charName.ToLowerInvariant();
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    _charToFile[key] = fi.FullName;
                }
            }
            catch { }
        }

        // ── Read "Listener:" from header ─────────────────────────────────
        private static string? ReadListenerHeader(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);
                // UTF-16-LE with BOM
                using var reader = new StreamReader(fs, EveEncoding,
                    detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);

                for (int i = 0; i < 20; i++)
                {
                    var line = reader.ReadLine();
                    if (line is null) break;
                    // Header line: "          Listener:        ARC Tendeuse A3"
                    var trimmed = line.TrimStart('﻿', ' ', '	');
                    if (trimmed.StartsWith("Listener:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Value after the colon, strip extra spaces
                        return trimmed["Listener:".Length..].Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        // ── Active EVE window → character name ───────────────────────────
        private static string? GetActiveEveCharacter()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            const string prefix = "EVE - ";
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return title[prefix.Length..].Trim();
            return null;
        }

        // ── Snapshot at startup ───────────────────────────────────────────
        public string? GetCurrentSystemSnapshot()
        {
            if (!Directory.Exists(LogDir)) return null;
            try
            {
                RebuildCharFileMap();
                foreach (var path in _charToFile.Values)
                {
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open,
                            FileAccess.Read, FileShare.ReadWrite);
                        long start = Math.Max(0, fs.Length - 16384);
                        if (start % 2 != 0) start--;
                        fs.Seek(start, SeekOrigin.Begin);
                        using var reader = new StreamReader(fs, EveEncoding, true, 4096, true);
                        string? line, latest = null;
                        while ((line = reader.ReadLine()) is not null)
                        {
                            var clean = line.TrimStart('﻿', ' ', '	');
                            var m = SystemRx.Match(clean);
                            if (m.Success) latest = m.Groups[1].Value.Trim();
                        }
                        if (latest is not null) _fileSystems[path] = latest;
                        _fileOffsets[path] = fs.Length;
                    }
                    catch { }
                }

                // Prefer active character's system
                var activeChar = GetActiveEveCharacter();
                if (activeChar is not null &&
                    _charToFile.TryGetValue(activeChar.ToLowerInvariant(), out var logPath) &&
                    _fileSystems.TryGetValue(logPath, out var sys))
                    return sys;

                // Fallback: most recently written
                var fallback = _charToFile.Values
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (fallback is not null &&
                    _fileSystems.TryGetValue(fallback, out var fbSys))
                    return fbSys;
            }
            catch { }
            return null;
        }

        public string? GetSystemForCharacter(string characterName)
        {
            var key = characterName.ToLowerInvariant();
            if (_charToFile.TryGetValue(key, out var path) &&
                _fileSystems.TryGetValue(path, out var sys))
                return sys;
            return null;
        }

        public void Dispose() => _timer.Dispose();
    }
}
