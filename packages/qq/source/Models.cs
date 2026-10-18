// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ChillQQMusicPlaylist
{
    public class Playlist { [JsonProperty("dissId")] public long id; [JsonProperty("dissName")] public string name; public int songCount; }
    public class UserPlaylists { public List<Playlist> created, collected; }
    public class PlaylistDetail { public List<Song> songs; public int songCount; }
    public class Song
    {
        [JsonProperty("mid")] public string id; [JsonProperty("id")] public long numericId; public string name; public List<string> artists; public double duration;
        public string Label { get { return name + "  /  " + (artists == null ? "" : string.Join("、", artists.ToArray())); } }
    }
    public class SongUrl { public string url; [JsonProperty("format")] public string type; public long size; }
    public class QrState { public int code = -1; public string msg; }
    public class User { public string nickname; }
    public static class Rules
    {
        public static string SecureMusicUrl(string value)
        {
            if (!IsMusicUrl(value)) return null;
            Uri uri = new Uri(value);
            if (uri.Scheme == "https") return uri.AbsoluteUri;
            return new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
        }
        public static bool IsMusicUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            string host = uri.DnsSafeHost.ToLowerInvariant();
            return host == "aqqmusic.tc.qq.com" || host == "stream.qqmusic.qq.com" || host.EndsWith(".stream.qqmusic.qq.com", StringComparison.Ordinal);
        }
        public static long PlaylistId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            long id;
            if (long.TryParse(value.Trim(), out id) && id > 0) return id;
            Uri uri;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || (uri.Host != "y.qq.com" && uri.Host != "c.y.qq.com")) return 0;
            Match match = Regex.Match(value, @"(?:/playlist/|[?&](?:id|disstid|dissid)=)(\d+)", RegexOptions.IgnoreCase);
            return match.Success && long.TryParse(match.Groups[1].Value, out id) && id > 0 ? id : 0;
        }
        public static List<Playlist> MergePlaylists(UserPlaylists page)
        {
            if (page == null) throw new InvalidOperationException("歌单响应为空。");
            List<Playlist> result = new List<Playlist> { new Playlist { id = -1, name = "我喜欢的音乐" } };
            HashSet<long> seen = new HashSet<long>();
            foreach (var list in new[] { page.created, page.collected })
                if (list != null) foreach (Playlist item in list)
                    if (item != null && item.id > 0 && seen.Add(item.id)) { item.name = item.name ?? "未命名歌单"; result.Add(item); }
            return result;
        }
        public static List<Song> UniqueSongs(List<Song> list)
        {
            List<Song> result = new List<Song>(); HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (list != null) foreach (Song item in list)
                if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)) { item.name = item.name ?? "未命名歌曲"; result.Add(item); }
            return result;
        }
    }
}
