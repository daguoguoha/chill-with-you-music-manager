// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ChillAudioProgress
{
    public sealed class Bookmark
    {
        public double Seconds, Duration;
        public long UpdatedUtc;
    }
    public sealed class ProgressDocument
    {
        public int Schema = 1;
        public Dictionary<string, Bookmark> Tracks = new Dictionary<string, Bookmark>(StringComparer.Ordinal);
    }
    public sealed class ProgressStore
    {
        readonly string path;
        readonly Action<string> warn;
        ProgressDocument data = new ProgressDocument();
        bool dirty, writable = true;
        public ProgressStore(string path, Action<string> warn)
        {
            this.path = path; this.warn = warn;
            if (!File.Exists(path)) return;
            try { data = Read(path); }
            catch (NotSupportedException) { writable = false; warn("进度文件来自更新版本，保留原文件并暂停写入。"); }
            catch (Exception)
            {
                // Keep the damaged original; never silently replace it with an empty document.
                try
                {
                    File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    if (File.Exists(path + ".bak")) data = Read(path + ".bak");
                    dirty = true;
                    warn("进度文件损坏，已保留原件；存在有效备份时已恢复备份。");
                }
                catch (Exception) { writable = false; warn("无法恢复进度文件，保留原文件并暂停写入。"); }
            }
        }
        static ProgressDocument Read(string file)
        {
            if (new FileInfo(file).Length > 4 * 1024 * 1024) throw new InvalidDataException();
            ProgressDocument result = JsonConvert.DeserializeObject<ProgressDocument>(File.ReadAllText(file, Encoding.UTF8));
            if (result == null || result.Tracks == null) throw new InvalidDataException();
            if (result.Schema != 1) throw new NotSupportedException();
            foreach (var item in result.Tracks)
                if (!Supported(item.Key) || item.Value == null || !Valid(item.Value.Seconds, item.Value.Duration)) throw new InvalidDataException();
            return result;
        }
        public static bool Supported(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 1024) return false;
            foreach (string prefix in new[] { "netease:", "qqmusic:", "kugou:", "bilibili:", "ximalaya:", "saltplayer:" })
                if (id.StartsWith(prefix, StringComparison.Ordinal) && id.Length > prefix.Length) return true;
            return false;
        }
        static bool Valid(double seconds, double duration)
        {
            return !double.IsNaN(seconds) && !double.IsInfinity(seconds) && !double.IsNaN(duration) && !double.IsInfinity(duration)
                && duration > 0 && seconds >= 0 && seconds <= duration;
        }
        public double Resume(string id, double duration)
        {
            Bookmark item;
            if (!Supported(id) || !data.Tracks.TryGetValue(id, out item) || !Valid(item.Seconds, duration)) return 0;
            // A different edit or local file replacement must not seek into unrelated content.
            if (Math.Abs(item.Duration - duration) > Math.Max(2, duration * 0.01)) return 0;
            return item.Seconds >= 3 && duration - item.Seconds > 5 ? item.Seconds : 0;
        }
        public void Record(string id, double seconds, double duration)
        {
            if (!writable || !Supported(id) || !Valid(seconds, duration)) return;
            if (seconds < 3 || duration - seconds <= 5) { Complete(id); return; }
            data.Tracks[id] = new Bookmark { Seconds = seconds, Duration = duration, UpdatedUtc = DateTime.UtcNow.Ticks };
            dirty = true;
        }
        public void Complete(string id)
        {
            if (writable && id != null && data.Tracks.Remove(id)) dirty = true;
        }
        public void Flush()
        {
            if (!writable || !dirty) return;
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (data.Tracks.Count > 2000)
                    data.Tracks = data.Tracks.OrderByDescending(p => p.Value.UpdatedUtc).Take(2000).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(data, Formatting.Indented));
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                dirty = false;
            }
            catch (Exception error) { warn("音频进度写入失败，将稍后重试：" + error.GetType().Name); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
    }
}
