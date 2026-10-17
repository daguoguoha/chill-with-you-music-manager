// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace ChillXimalayaPlaylist
{
    public class Playlist {public string id,name;public int songCount;}
    public class UserPlaylists {public List<Playlist> created,collected;}
    public class Song {public string id,name;public List<string> artists;public double duration;public string Label{get{return name+"  /  "+(artists==null?"":string.Join("、",artists.ToArray()));}}}
    public class SongUrl {public string url,type;public long size;}
    public class QrState {public int code=-1;public string msg;}
    public class User {public string nickname;}
    public static class Rules
    {
        static bool Id(string value){long n;return value!=null && Regex.IsMatch(value,@"^[1-9]\d{0,17}$") && long.TryParse(value,out n);}
        public static bool IsMusicUrl(string value)
        {
            Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http") || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))return false;
            return uri.DnsSafeHost.EndsWith(".xmcdn.com",StringComparison.OrdinalIgnoreCase);
        }
        public static string SecureMusicUrl(string value){if(!IsMusicUrl(value))return null;Uri uri=new Uri(value);return uri.Scheme=="https"?uri.AbsoluteUri:new UriBuilder(uri){Scheme="https",Port=-1}.Uri.AbsoluteUri;}
        static string Param(Uri uri,string name){foreach(string part in uri.Query.TrimStart('?').Split('&')){string[] bits=part.Split(new[]{'='},2);if(bits.Length==2 && Uri.UnescapeDataString(bits[0])==name)return Uri.UnescapeDataString(bits[1]);}return null;}
        public static string PlaylistId(string input)
        {
            if(string.IsNullOrWhiteSpace(input))return null;string value=input.Trim();
            if(value=="likes")return value;
            if(value.StartsWith("album:",StringComparison.Ordinal) && Id(value.Substring(6)))return value;
            if(value.StartsWith("track:",StringComparison.Ordinal) && Id(value.Substring(6)))return value;
            if(Id(value))return "album:"+value;
            if(value.StartsWith("short:",StringComparison.Ordinal))value=value.Substring(6);
            Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http") || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)return null;
            if(uri.Host=="xima.tv")return "short:"+new UriBuilder(uri){Scheme="https",Port=-1}.Uri.AbsoluteUri;
            if(uri.Host!="www.ximalaya.com" && uri.Host!="m.ximalaya.com" && uri.Host!="ximalaya.com")return null;
            Match match=Regex.Match(uri.AbsolutePath,@"/(?:share/)?(?:sound|track)/(\d+)(?:/|$)");
            if(match.Success && Id(match.Groups[1].Value))return "track:"+match.Groups[1].Value;
            match=Regex.Match(uri.AbsolutePath,@"/(?:share/)?album/(\d+)(?:/|$)");
            if(match.Success && Id(match.Groups[1].Value))return "album:"+match.Groups[1].Value;
            match=Regex.Match(uri.AbsolutePath,@"/tracks/(\d+)\.json$");
            if(match.Success && Id(match.Groups[1].Value))return "track:"+match.Groups[1].Value;
            match=Regex.Match(uri.AbsolutePath,@"/[a-zA-Z]+/(\d+)/(\d+)(?:/|$)");
            if(match.Success && Id(match.Groups[2].Value))return "track:"+match.Groups[2].Value;
            foreach(string name in new[]{"trackId","track_id","sound_id","albumId","album_id"}){string id=Param(uri,name);if(Id(id))return (name.StartsWith("album",StringComparison.Ordinal)?"album:":"track:")+id;}
            if(uri.AbsolutePath.StartsWith("/short/",StringComparison.Ordinal))return "short:"+new UriBuilder(uri){Scheme="https",Port=-1}.Uri.AbsoluteUri;
            return null;
        }
        public static List<Playlist> MergePlaylists(UserPlaylists page)
        {
            if(page==null)throw new InvalidOperationException("订阅响应为空。");var result=new List<Playlist>();var seen=new HashSet<string>();
            foreach(var list in new[]{page.created,page.collected})if(list!=null)foreach(var item in list)if(item!=null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)){if(string.IsNullOrEmpty(item.name))item.name="未命名专辑";result.Add(item);}return result;
        }
        public static List<Song> UniqueSongs(List<Song> list)
        {
            var result=new List<Song>();var seen=new HashSet<string>();if(list!=null)foreach(var item in list)if(item!=null && !string.IsNullOrEmpty(item.id) && seen.Add(item.id)){if(string.IsNullOrEmpty(item.name))item.name="未命名声音";result.Add(item);}return result;
        }
    }
}
