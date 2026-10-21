// SPDX-License-Identifier: GPL-3.0-or-later
// Public lyric protocol references: QQ bridge api/song.go; KuGouMusicApi module/search_lyric.js and lyric.js.
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ChillNeteasePlaylist
{
    internal sealed class LyricTrack
    {
        internal string Key, Title, Id, Hash, LocalPath;
        internal double Duration;
    }
    internal sealed class LyricResult
    {
        internal LyricDocument Document = new LyricDocument();
        internal LyricDocument Translation = new LyricDocument();
        internal string Message;
        internal bool FromLocal;
        internal JObject PlatformLyrics;
    }
    internal static class LyricSource
    {
        internal const int MaxBytes = 2 * 1024 * 1024;
        internal static Func<long, CancellationToken, JObject> NeteaseRequest;
        static JObject NeteaseLyrics(long id, CancellationToken cancel)
        {
            if (NeteaseRequest != null) return NeteaseRequest(id, cancel);
            return Get("https://music.163.com/api/song/lyric?id=" + id.ToString(CultureInfo.InvariantCulture) + "&lv=-1&kv=-1&tv=-1", "https://music.163.com/", cancel);
        }
        internal static string CacheName(string key)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant() + ".lrc";
        }
        internal static string ReadLocal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请先填写 LRC 文件路径。");
            path = path.Trim().Trim('"');
            if (path.Length < 3 || !char.IsLetter(path[0]) || path[1] != ':' || (path[2] != '\\' && path[2] != '/') || path.Substring(2).Contains(":"))
                throw new InvalidOperationException("请选择本机磁盘上的 LRC 文件。");
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".lrc" && ext != ".txt") throw new InvalidOperationException("仅支持 LRC 或歌词 TXT 文件。");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length > MaxBytes) throw new InvalidOperationException("歌词文件不能超过 2 MB。");
                return Decode(ReadBounded(stream));
            }
        }
        static byte[] ReadBounded(Stream stream)
        {
            using (var bytes = new MemoryStream())
            {
                byte[] buffer = new byte[8192]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                { if (bytes.Length + count > MaxBytes) throw new InvalidOperationException("歌词响应过大。"); bytes.Write(buffer, 0, count); }
                return bytes.ToArray();
            }
        }
        internal static string Decode(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(54936).GetString(bytes); }
        }
        static void Save(string path, string raw)
        {
            if (raw.Length > LyricDocument.MaxCharacters) throw new InvalidOperationException("歌词过大。");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, raw, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static LyricResult Import(string cacheDirectory, LyricTrack track, string file, string pasted, CancellationToken cancel)
        {
            string raw = string.IsNullOrWhiteSpace(pasted) ? ReadLocal(file) : pasted;
            cancel.ThrowIfCancellationRequested();
            LyricDocument document = LyricDocument.Parse(raw);
            if (document.Lines.Count == 0) throw new InvalidOperationException("文件中没有可显示的歌词。");
            Save(Path.Combine(cacheDirectory, CacheName(track.Key)), raw);
            return new LyricResult { Document = document, FromLocal = true, Message = document.Timed ? "已导入同步歌词" : "已导入纯文本歌词 · 无时间轴" };
        }
        internal static LyricResult Load(string provider, string cacheDirectory, LyricTrack track, bool refresh, CancellationToken cancel)
        {
            string cache = Path.Combine(cacheDirectory, CacheName(track.Key));
            if (!refresh && File.Exists(cache))
            {
                try { var saved = LyricDocument.Parse(ReadLocal(cache)); if (saved.Lines.Count > 0) return new LyricResult { Document = saved, FromLocal = true, Message = saved.Timed ? "本地同步歌词" : "本地歌词 · 无时间轴" }; }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (InvalidOperationException) { }
            }
            cancel.ThrowIfCancellationRequested();
            if (provider == "saltplayer" && !string.IsNullOrEmpty(track.LocalPath))
            {
                string sidecar = Path.ChangeExtension(track.LocalPath, ".lrc");
                if (File.Exists(sidecar)) return Import(cacheDirectory, track, sidecar, null, cancel);
            }
            JObject platformLyrics = null;
            string raw;
            long neteaseId;
            if (provider == "netease" && long.TryParse(track.Id, out neteaseId) && neteaseId > 0)
            { platformLyrics = NeteaseLyrics(neteaseId, cancel); raw = (string)platformLyrics.SelectToken("lrc.lyric"); }
            else raw = Fetch(provider, track, cancel);
            cancel.ThrowIfCancellationRequested();
            LyricDocument parsed = LyricDocument.Parse(raw);
            if (parsed.Lines.Count == 0) return new LyricResult { Message = "暂无歌词 · 可在歌词设置中导入 LRC" };
            try { Save(cache, raw); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return new LyricResult { Document = parsed, PlatformLyrics = platformLyrics, Message = parsed.Timed ? "自动同步歌词" : "纯文本歌词 · 无时间轴" };
        }
        internal static string TranslationCacheName(string key)
        { return Path.ChangeExtension(CacheName(key),"translation.lrc"); }
        internal static LyricResult WithTranslation(LyricResult original,string provider,string directory,LyricTrack track,bool refresh,CancellationToken cancel)
        {
            string cache=Path.Combine(directory,TranslationCacheName(track.Key));
            cancel.ThrowIfCancellationRequested();
            // Explicitly imported translations always win; refresh only retries an absent source.
            if(File.Exists(cache))
            {
                try { original.Translation=LyricDocument.Parse(ReadLocal(cache)); }
                catch(IOException){} catch(UnauthorizedAccessException){} catch(InvalidOperationException){}
            }
            if((!original.Translation.HasText || !original.Translation.Timed) && provider=="netease")
            {
                try
                {
                    long id;
                    if(long.TryParse(track.Id,out id) && id>0)
                    {
                        var json=original.PlatformLyrics ?? NeteaseLyrics(id,cancel);
                        string raw=(string)json.SelectToken("tlyric.lyric");
                        original.Translation=LyricDocument.Parse(raw);
                        cancel.ThrowIfCancellationRequested();
                        if(original.Translation.HasText && original.Translation.Timed)try{Save(cache,raw);}catch(IOException){}catch(UnauthorizedAccessException){}
                    }
                }
                catch(WebException){cancel.ThrowIfCancellationRequested();}
                catch(Newtonsoft.Json.JsonException){} catch(InvalidOperationException){}
            }
            cancel.ThrowIfCancellationRequested();
            original.Message+=original.Translation.HasText && original.Translation.Timed ? " · 已载入译文" : " · 暂无译文，可导入翻译LRC";
            return original;
        }
        internal static LyricResult ImportTranslation(LyricResult original,string directory,LyricTrack track,string file,string pasted,CancellationToken cancel)
        {
            string raw=string.IsNullOrWhiteSpace(pasted)?ReadLocal(file):pasted;
            cancel.ThrowIfCancellationRequested();
            LyricDocument translated=LyricDocument.Parse(raw);
            if(!translated.Timed || !translated.HasText)throw new InvalidOperationException("翻译歌词需要带时间戳的LRC，请使用与原文相同的时间轴。");
            Save(Path.Combine(directory,TranslationCacheName(track.Key)),raw);
            original.Translation=translated;original.Message="已导入译文 · 开启翻译后显示，原文保持不变";
            return original;
        }
        internal static string DecodeBase64(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (value.Length > MaxBytes) throw new InvalidOperationException("歌词响应过大。");
            return WebUtility.HtmlDecode(Decode(Convert.FromBase64String(value)));
        }
        internal static string Fetch(string provider, LyricTrack track, CancellationToken cancel)
        {
            if (provider == "netease")
            {
                long id; if (!long.TryParse(track.Id, out id) || id <= 0) return null;
                var json = NeteaseLyrics(id,cancel);
                return (string)json.SelectToken("lrc.lyric");
            }
            if (provider == "qq")
            {
                if (string.IsNullOrEmpty(track.Id) || track.Id.Length > 64) return null;
                var json = Get("https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?songmid=" + Uri.EscapeDataString(track.Id) + "&format=json&nobase64=0", "https://y.qq.com/portal/player.html", cancel);
                if ((int?)json["code"] != 0 && (int?)json["retcode"] != 0) return null;
                return DecodeBase64((string)json["lyric"]);
            }
            if (provider == "kugou")
            {
                if (string.IsNullOrEmpty(track.Hash) || !System.Text.RegularExpressions.Regex.IsMatch(track.Hash, "^[a-fA-F0-9]{32}$")) return null;
                var json = Get("https://lyrics.kugou.com/v1/search?appid=1005&clientver=20489&hash=" + track.Hash + "&duration=" + Math.Max(0, Math.Min(86400000, track.Duration * 1000)).ToString("0", CultureInfo.InvariantCulture) + "&lrctxt=1&man=no", "https://www.kugou.com/", cancel);
                var candidates = json["candidates"] as JArray;
                if (candidates == null || candidates.Count == 0) return null;
                // The search is by the exact audio hash, never by a guessed song title.
                string id = (string)candidates[0]["id"], access = (string)candidates[0]["accesskey"];
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(access) || id.Length > 64 || access.Length > 256) return null;
                var lyric = Get("https://lyrics.kugou.com/download?ver=1&client=android&fmt=lrc&charset=utf8&id=" + Uri.EscapeDataString(id) + "&accesskey=" + Uri.EscapeDataString(access), "https://www.kugou.com/", cancel);
                return DecodeBase64((string)lyric["content"]);
            }
            return null;
        }
        internal static JObject Get(string url, string referer, CancellationToken cancel)
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || (uri.Host != "music.163.com" && uri.Host != "c.y.qq.com" && uri.Host != "lyrics.kugou.com")) throw new InvalidOperationException("歌词地址不受支持。");
            cancel.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET"; request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            request.AllowAutoRedirect = false; request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.UserAgent = "Mozilla/5.0"; request.Referer = referer;
            using (cancel.Register(delegate { request.Abort(); }))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaxBytes) throw new InvalidOperationException("歌词接口暂不可用。");
                string text = Decode(ReadBounded(stream));
                if (text.Length > LyricDocument.MaxCharacters) throw new InvalidOperationException("歌词响应过大。");
                return JObject.Parse(text);
            }
        }
    }
}
