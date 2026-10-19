// SPDX-License-Identifier: GPL-3.0-or-later
// Endpoint adapted from MakcRe/KuGouMusicApi (MIT), everyday_recommend.js.
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace ChillKuGouPlaylist {
    public static partial class Bridge {
        internal static void RememberSong(Song song) {
            if(song==null || !System.Text.RegularExpressions.Regex.IsMatch(song.hash??"","^[a-fA-F0-9]{32}$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(song.albumId??"","^[0-9]{1,20}$") || song.albumAudioId<0 ||
                song.id!=song.hash.ToLowerInvariant()+":"+song.albumId+":"+song.albumAudioId)
                throw new InvalidOperationException("歌曲元数据无效，请重新读取推荐或歌单。");
            knownSongs[song.id]=song;
        }
        public static string SongURL(Song song) {
            RememberSong(song);
            return SongURL(song.id);
        }
        public static List<Song> RecommendSongs() {
            if (!IsLoggedIn) throw new InvalidOperationException("请先登录酷狗再读取每日推荐。");
            JObject response=Api("/everyday_song_recommend",Params("platform","ios"),null,"everydayrec.service.kugou.com",true);
            List<Song> songs=ParseRecommendations(response);
            foreach(var song in songs) knownSongs[song.id]=song;
            return songs;
        }
        internal static List<Song> ParseRecommendations(JObject response) {
            var data=response == null ? null : response["data"];
            JArray rows=data as JArray ?? (data == null ? null : data["song_list"] as JArray ?? data["songs"] as JArray ?? data["list"] as JArray);
            if(rows==null) throw new InvalidOperationException("酷狗推荐响应格式不受支持，请稍后重试。");
            if(rows.Count>2000) throw new InvalidOperationException("酷狗推荐响应过大。");
            var songs=new List<Song>();var seen=new HashSet<string>();
            foreach(var token in rows) {
                JObject row=token as JObject;if(row==null)continue;
                string hash=Text(row["hash"] ?? row["filehash"]);
                if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-fA-F0-9]{32}$"))continue;
                string album=Text(row["album_id"]);if(album.Length==0)album="0";
                long audio=Number(row["mixsongid"] ?? row["album_audio_id"] ?? row["audio_id"]);
                string key=hash.ToLowerInvariant()+":"+album+":"+audio;
                if(!seen.Add(key))continue;
                var artists=new List<string>();JArray singers=row["singerinfo"] as JArray ?? row["singerinfos"] as JArray;
                if(singers!=null)foreach(var singer in singers){string name=Text(singer["name"]);if(name.Length>0)artists.Add(name);}
                string filename=Text(row["filename"] ?? row["name"]),title=Text(row["songname"] ?? row["song_name"]);
                int dash=filename.IndexOf(" - ",StringComparison.Ordinal);
                if(title.Length==0)title=dash>=0?filename.Substring(dash+3):filename;
                if(artists.Count==0){string singer=Text(row["author_name"] ?? row["singername"]);if(singer.Length>0)artists.Add(singer);else if(dash>0)artists.Add(filename.Substring(0,dash));}
                double seconds=Number(row["timelen"])/1000.0;if(seconds<=0)seconds=Number(row["duration"]);if(seconds<=0)seconds=Number(row["time_length"]);
                songs.Add(new Song{id=key,name=title,hash=hash.ToLowerInvariant(),albumId=album,albumAudioId=audio,artists=artists,duration=seconds});
            }
            if(songs.Count==0)throw new InvalidOperationException("酷狗暂未返回推荐歌曲，请稍后重试。");
            return songs;
        }
    }
}
