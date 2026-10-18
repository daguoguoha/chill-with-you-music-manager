// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using ChillNeteasePlaylist;
class SessionStoreTests
{
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static PlaybackSession Record(string provider, long time) { return new PlaybackSession { Provider = provider, CurrentId = "7", PlaylistId = "11", PlaylistTitle = "Synthetic", QueueJson = "[{\"id\":7,\"name\":\"Synthetic\"}]", Seconds = 34, Duration = 120, Paused = true, UpdatedUtcTicks = time }; }
    static void Main()
    {
        string path = Path.Combine(Path.GetTempPath(), "chill-session-test-" + Guid.NewGuid().ToString("N"), "last.json");
        Check(PlaybackSessionStore.Read(path) == null, "missing session is a normal first run");
        var record = Record("netease", 10);
        Check(PlaybackSessionStore.Valid(record), "valid paused session");
        Check(PlaybackSessionStore.Write(path, record), "initial session written");
        var read = PlaybackSessionStore.Read(path);
        Check(read.Provider == "netease" && read.Paused && read.Seconds == 34 && read.PlaylistId == "11", "track playlist position and pause survive restart");
        Check(PlaybackSessionStore.Position(read, 120) == 34, "valid position restored");
        Check(PlaybackSessionStore.Position(read, 300) == 0, "changed audio duration rejects old position");
        read.Seconds = 2; Check(PlaybackSessionStore.Position(read, 120) == 0, "tiny bookmark restarts at beginning");
        read.Seconds = 118; Check(PlaybackSessionStore.Position(read, 120) == 0, "finished audio does not resume at end");
        Check(PlaybackSessionStore.Write(path, Record("qq", 20)), "switching provider records newest platform");
        Check(!PlaybackSessionStore.Write(path, Record("netease", 15)) && PlaybackSessionStore.Read(path).Provider == "qq", "late old-provider writer cannot overwrite new provider");
        Check(File.Exists(path + ".bak"), "atomic update retains previous backup");
        var invalid = Record("netease", 30); invalid.Schema = 99; Check(!PlaybackSessionStore.Valid(invalid) && !PlaybackSessionStore.Write(path, invalid), "unsupported schema is not written");
        invalid = Record("netease", 30); invalid.Seconds = double.NaN; Check(!PlaybackSessionStore.Valid(invalid), "NaN progress rejected");
        invalid = Record("netease", 30); invalid.Duration = double.PositiveInfinity; Check(!PlaybackSessionStore.Valid(invalid), "infinite duration rejected");
        invalid = Record("unknown", 30); Check(!PlaybackSessionStore.Valid(invalid), "unknown provider rejected");
        using (var hold = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            bool locked = false; try { PlaybackSessionStore.Write(path, Record("kugou", 30)); } catch (IOException) { locked = true; }
            Check(locked && PlaybackSessionStore.Read(path).Provider == "qq", "cross-plugin file lock prevents concurrent overwrite");
        }
        var original = Record("game", 40); original.CurrentId = "game-original"; original.QueueJson = "[]"; original.Seconds = 0; original.Duration = 1;
        Check(PlaybackSessionStore.Write(path, original) && PlaybackSessionStore.Read(path).Provider == "game", "original game music can suppress old cloud auto-play");
        float r, g, b;
        Check(LyricPalette.TryRgb("#FF0080", out r, out g, out b) && r == 1 && g == 0 && Math.Abs(b - 128f / 255) < 0.001, "custom RGB color parsed");
        Check(LyricPalette.TryRgb("#aaddff", out r, out g, out b), "lowercase color accepted");
        Check(!LyricPalette.TryRgb("#bad", out r, out g, out b), "short color rejected");
        Check(!LyricPalette.TryRgb("#GG0000", out r, out g, out b), "invalid hex rejected");
        Check(!LyricPalette.TryRgb(null, out r, out g, out b), "missing color rejected");
        Console.WriteLine("TOTAL " + passed);
    }
}
