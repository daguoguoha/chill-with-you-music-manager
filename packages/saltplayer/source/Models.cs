// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace ChillSaltPlayerPlaylist
{
    public class Playlist {public string id,name,source,root;public int songCount;public List<Song> songs=new List<Song>();}
    public class Song {public string id,name,path;public List<string> artists;public double duration;public string Label{get{return name+"  /  "+(artists==null?"":string.Join("、",artists.ToArray()));}}}
    public class Library {public int version=1;public List<Playlist> playlists=new List<Playlist>();}
    public class ImportResult {public Playlist playlist;public int missing,ambiguous,unsupported;public List<string> details=new List<string>();public string Summary{get{return "导入 "+playlist.songs.Count+" 首；缺失 "+missing+"，重名待确认 "+ambiguous+"，不支持 "+unsupported+"。";}}}
    public static class Rules
    {
        public static readonly string[] AudioExtensions={".mp3",".flac",".wav",".m4a",".aac",".wma"};
        public static bool IsAudio(string path){try{return path!=null && Array.IndexOf(AudioExtensions,Path.GetExtension(path).ToLowerInvariant())>=0;}catch(ArgumentException){return false;}}
        public static string LocalPath(string input,string relativeTo)
        {
            if(string.IsNullOrWhiteSpace(input))return null;
            string value=input.Trim().Trim('"');
            try
            {
                if(value.StartsWith("file:",StringComparison.OrdinalIgnoreCase)){Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || !uri.IsFile || uri.IsUnc || !string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.Query))return null;value=uri.LocalPath;}
                if(value.StartsWith(@"\\",StringComparison.Ordinal) || value.IndexOf('\0')>=0)return null;
                if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^[A-Za-z]:[\\/]"))
                {if(value.Contains(":") || value.StartsWith("/",StringComparison.Ordinal) || string.IsNullOrEmpty(relativeTo))return null;value=Path.Combine(relativeTo,value.Replace('/',Path.DirectorySeparatorChar));}
                string full=Path.GetFullPath(value);
                if(full.Substring(2).Contains(":"))return null;return full;
            }
            catch(ArgumentException){return null;}catch(NotSupportedException){return null;}catch(PathTooLongException){return null;}
        }
        public static List<Song> UniqueSongs(List<Song> songs){return songs ?? new List<Song>();}
    }
}
