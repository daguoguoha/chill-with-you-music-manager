// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace ChillKuGouPlaylist
{
    public class Playlist { public string id, name; public int songCount; }
    public class UserPlaylists { public List<Playlist> created, collected; }
    public class Song
    {
        public string id, name, hash, albumId; public long albumAudioId, fileId; public List<string> artists; public double duration;
        public string Label { get { return name + "  /  " + (artists == null ? "" : string.Join("、", artists.ToArray())); } }
    }
    public class SongUrl { public string url, type; public long size; }
    public class QrState { public int code = -1; public string msg; }
    public class User { public string nickname; }
    public static class Rules
    {
        public static bool IsMusicUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
            string host = uri.DnsSafeHost.ToLowerInvariant();
            return host == "fsandroid.kugou.com" || host == "fsandroidtx.kugou.com" || host == "fsmobile.kugou.com" || host == "fsmobile.tx.kugou.com" || host == "fs.kugou.com" || host == "webfs.tx.kugou.com";
        }
        public static string SecureMusicUrl(string value)
        {
            if (!IsMusicUrl(value)) return null;
            Uri uri = new Uri(value); return uri.Scheme == "https" ? uri.AbsoluteUri : new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri;
        }
        public static string PlaylistId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (Regex.IsMatch(value, @"^collection_\d+_\d+_\d+_\d+$")) return value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) || !(uri.Host == "kugou.com" || uri.Host.EndsWith(".kugou.com", StringComparison.OrdinalIgnoreCase))) return null;
            Match match = Regex.Match(Uri.UnescapeDataString(uri.Query), @"(?:^|[?&])(?:global_collection_id|gci|id)=(collection_\d+_\d+_\d+_\d+)(?:&|$)");
            return match.Success ? match.Groups[1].Value : null;
        }
        public static List<Playlist> MergePlaylists(UserPlaylists page)
        {
            if (page == null) throw new InvalidOperationException("歌单响应为空。");
            var result = new List<Playlist>(); var seen = new HashSet<string>();
            foreach (var list in new[] { page.created, page.collected })
                if (list != null) foreach (Playlist item in list)
                    if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)) { if (string.IsNullOrEmpty(item.name)) item.name = "未命名歌单"; result.Add(item); }
            return result;
        }
        public static List<Song> UniqueSongs(List<Song> list)
        {
            var result = new List<Song>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            if (list != null) foreach (Song item in list)
                if (item != null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)) { if (string.IsNullOrEmpty(item.name)) item.name = "未命名歌曲"; result.Add(item); }
            return result;
        }
    }
}
