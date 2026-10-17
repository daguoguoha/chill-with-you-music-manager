// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChillBilibiliPlaylist
{
    public class Playlist { public string id, name; public int songCount; }
    public class UserPlaylists { public List<Playlist> created, collected; }
    public class Song
    {
        public string id, name, bvid; public long cid; public List<string> artists; public double duration;
        public string Label { get { return name + "  /  " + (artists == null ? "" : string.Join("、", artists.ToArray())); } }
    }
    public class SongUrl { public string url, type; public long size; }
    public class QrState { public int code = -1; public string msg; }
    public class User { public string nickname; }
    public static class Rules
    {
        public static bool IsBvid(string value) { return value != null && Regex.IsMatch(value, "^BV[a-zA-Z0-9]{10}$"); }
        public static bool IsMusicUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
            string host = uri.DnsSafeHost.ToLowerInvariant();
            return host.EndsWith(".bilivideo.com", StringComparison.Ordinal) || host.EndsWith(".bilivideo.cn", StringComparison.Ordinal);
        }
        public static string SecureMusicUrl(string value)
        {
            if (!IsMusicUrl(value)) return null;
            Uri uri = new Uri(value); return uri.Scheme == "https" ? uri.AbsoluteUri : new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
        }
        static string Param(Uri uri, string name)
        {
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                string[] bits = part.Split(new[] { '=' }, 2);
                if (bits.Length == 2 && Uri.UnescapeDataString(bits[0]) == name) return Uri.UnescapeDataString(bits[1]);
            }
            return null;
        }
        public static string PlaylistId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (IsBvid(value)) return "video:" + value + ":all";
            if (Regex.IsMatch(value, @"^fav:\d{1,20}$")) return value;
            if (Regex.IsMatch(value, @"^\d{1,20}$")) return "fav:" + value;
            if (Regex.IsMatch(value, @"^video:BV[a-zA-Z0-9]{10}:(?:all|[1-9]\d{0,3})$")) return value;
            if (value.StartsWith("short:", StringComparison.Ordinal)) value = value.Substring(6);
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return null;
            if (uri.Host.Equals("b23.tv", StringComparison.OrdinalIgnoreCase)) return "short:" + new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
            if (uri.Host != "www.bilibili.com" && uri.Host != "bilibili.com" && uri.Host != "m.bilibili.com" && uri.Host != "space.bilibili.com") return null;
            string fid = Param(uri,"fid");
            if (fid != null && Regex.IsMatch(fid,@"^\d{1,20}$")) return "fav:" + fid;
            Match folder = Regex.Match(uri.AbsolutePath,@"/(?:list|medialist/detail)/ml(\d{1,20})(?:/|$)");
            if (folder.Success) return "fav:" + folder.Groups[1].Value;
            Match video = Regex.Match(uri.AbsolutePath,@"/video/(BV[a-zA-Z0-9]{10})(?:/|$)");
            if (!video.Success) return null;
            string page = Param(uri,"p");
            if (page != null && !Regex.IsMatch(page,@"^[1-9]\d{0,3}$")) return null;
            return "video:" + video.Groups[1].Value + ":" + (page ?? "all");
        }
        public static List<Playlist> MergePlaylists(UserPlaylists page)
        {
            if (page == null) throw new InvalidOperationException("收藏夹响应为空。");
            var result = new List<Playlist>(); var seen = new HashSet<string>();
            foreach (var list in new[] { page.created, page.collected })
                if (list != null) foreach (Playlist item in list)
                    if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)) { if (string.IsNullOrEmpty(item.name)) item.name = "未命名收藏夹"; result.Add(item); }
            return result;
        }
        public static List<Song> UniqueSongs(List<Song> list)
        {
            var result = new List<Song>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            if (list != null) foreach (Song item in list)
                if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)) { if (string.IsNullOrEmpty(item.name)) item.name = "未命名视频"; result.Add(item); }
            return result;
        }
    }
}
