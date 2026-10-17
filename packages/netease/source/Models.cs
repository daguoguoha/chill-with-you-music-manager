// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChillNeteasePlaylist
{
    public class Playlist { public long id; public string name; public int songCount; }
    public class PlaylistPage { public List<Playlist> playlists; public bool hasMore; }
    public class Song
    {
        public long id; public string name; public List<string> artists; public double duration;
        public string Label { get { return name + "  /  " + (artists == null ? "" : string.Join("、", artists.ToArray())); } }
    }
    public class SongUrl { public string url; public string type; public bool isTrial; public long size; }
    public class QrState { public string qrcodeUrl; public int statusCode; public string statusMsg; }
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
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return false;
            string host = uri.DnsSafeHost.ToLowerInvariant();
            return host.EndsWith(".music.126.net") || host.EndsWith(".music.163.com") || host == "music.163.com";
        }
        public static long PlaylistId(string value)
        {
            long id;
            if (long.TryParse(value.Trim(), out id) && id > 0) return id;
            Match match = Regex.Match(value, @"(?:playlist[^?]*\?[^#]*?\bid=|/playlist/)(\d+)", RegexOptions.IgnoreCase);
            return match.Success && long.TryParse(match.Groups[1].Value, out id) && id > 0 ? id : 0;
        }
    }
}
