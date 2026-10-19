// SPDX-License-Identifier: GPL-3.0-or-later
// Cloud playlist operations follow MakcRe/KuGouMusicApi (MIT).
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace ChillKuGouPlaylist {
    public static partial class Bridge {
        static string favoritePlaylist;
        static Dictionary<string,Song> favoriteSongs=new Dictionary<string,Song>(StringComparer.OrdinalIgnoreCase);
        public static string FavoriteKey(Song song){return song==null?"":(song.hash??"").ToLowerInvariant();}
        public static HashSet<string> GetFavoriteIds(){
            if(!IsLoggedIn)throw new InvalidOperationException("请先登录酷狗。");
            var page=JsonConvert.DeserializeObject<UserPlaylists>(UserPlaylists());
            var choices=(page.created??new List<Playlist>()).Where(p=>p.id!=null && p.id.StartsWith("mine:",StringComparison.Ordinal) && (p.name=="我喜欢" || p.name=="我喜欢的音乐")).ToList();
            if(choices.Count!=1)throw new InvalidOperationException("无法唯一确认酷狗的我喜欢歌单，未执行更改。");
            var list=Playlist(choices[0].id);
            var next=new Dictionary<string,Song>(StringComparer.OrdinalIgnoreCase);
            foreach(var song in list){string key=FavoriteKey(song);if(key.Length>0)next[key]=song;}
            favoritePlaylist=choices[0].id;favoriteSongs=next;
            return new HashSet<string>(next.Keys,StringComparer.OrdinalIgnoreCase);
        }
        public static void SetFavorite(Song song,bool desired){
            RememberSong(song);
            // Re-read the account list before a write, including the server's file IDs.
            GetFavoriteIds();
            string[] parts=favoritePlaylist.Split(':');
            if(parts.Length!=3 || parts[2]!="0")throw new InvalidOperationException("酷狗喜欢歌单类型尚不支持，未执行更改。");
            Song existing;bool liked=favoriteSongs.TryGetValue(FavoriteKey(song),out existing);
            if(liked==desired)return;
            JObject body=new JObject{{"userid",account["userid"]},{"token",account["token"]},{"listid",long.Parse(parts[1])},{"list_ver",0},{"type",0}};
            if(desired){
                body["slow_upload"]=1;body["scene"]="false;null";
                body["data"]=new JArray(new JObject{{"number",1},{"name",song.Label},{"hash",song.hash},{"size",0},{"sort",0},{"timelen",0},{"bitrate",0},{"album_id",long.Parse(song.albumId)},{"mixsongid",song.albumAudioId}});
                long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;
                Api("/cloudlist.service/v6/add_song",Params("last_time",now.ToString(System.Globalization.CultureInfo.InvariantCulture),"last_area","gztx","userid",Text(account["userid"]),"token",Text(account["token"])),body,null);
            }else{
                if(existing.fileId<=0)throw new InvalidOperationException("未获取到喜欢歌曲的文件 ID，未执行删除。");
                body["data"]=new JArray(new JObject{{"fileid",existing.fileId}});
                Api("/v4/delete_songs",null,body,"cloudlist.service.kugou.com");
            }
        }
    }
}
