// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
namespace ChillXimalayaPlaylist
{
    internal sealed class PlaybackSession
    {
        public int Schema = 1;
        public string Provider, CurrentId, PlaylistId, PlaylistTitle, QueueJson;
        public double Seconds, Duration;
        public bool Paused;
        public long UpdatedUtcTicks;
    }
    internal static class PlaybackSessionStore
    {
        internal const int MaxBytes = 2 * 1024 * 1024;
        static readonly string[] Providers = { "netease", "qq", "kugou", "bilibili", "ximalaya", "saltplayer", "game" };
        internal static bool Valid(PlaybackSession value)
        {
            return value != null && value.Schema == 1 && Array.IndexOf(Providers, value.Provider) >= 0 &&
                !string.IsNullOrEmpty(value.CurrentId) && value.CurrentId.Length <= 200 && !string.IsNullOrEmpty(value.QueueJson) && value.QueueJson.Length < MaxBytes &&
                !double.IsNaN(value.Seconds) && !double.IsInfinity(value.Seconds) && value.Seconds >= 0 && value.Seconds <= 86400 &&
                !double.IsNaN(value.Duration) && !double.IsInfinity(value.Duration) && value.Duration > 0 && value.Duration <= 86400;
        }
        internal static PlaybackSession Read(string path)
        {
            if (!File.Exists(path)) return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > MaxBytes) return null;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string text = reader.ReadToEnd();
                    if (text.Length > MaxBytes) return null;
                    PlaybackSession record = JsonConvert.DeserializeObject<PlaybackSession>(text);
                    return Valid(record) ? record : null;
                }
            }
        }
        internal static bool Write(string path, PlaybackSession record)
        {
            if (!Valid(record)) return false;
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            // All six plugin assemblies share this file lock. Only the active provider writes.
            using (var fileLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                PlaybackSession old = null;
                try { old = Read(path); } catch (JsonException) { } catch (IOException) { }
                if (old != null && old.UpdatedUtcTicks > record.UpdatedUtcTicks) return false;
                string json = JsonConvert.SerializeObject(record);
                if (Encoding.UTF8.GetByteCount(json) > MaxBytes) return false;
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, json, new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return true;
            }
        }
        internal static double Position(PlaybackSession record, double duration)
        {
            if (!Valid(record) || double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 || Math.Abs(duration - record.Duration) > Math.Max(5, record.Duration * 0.03)) return 0;
            if (record.Seconds < 3 || record.Seconds >= duration - 5) return 0;
            return record.Seconds;
        }
    }
}
