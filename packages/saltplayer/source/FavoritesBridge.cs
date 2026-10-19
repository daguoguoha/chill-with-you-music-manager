// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
namespace ChillSaltPlayerPlaylist {
    public static partial class Bridge {
        sealed class FavoriteLibrary { public int version=1;public List<Song> songs=new List<Song>(); }
        static FavoriteLibrary favorites=new FavoriteLibrary();
        public const string FavoritesPlaylist="local:favorites";
        public static string FavoriteKey(Song song){string path=song==null?null:Rules.LocalPath(song.path,null);return path==null || !Rules.IsAudio(path)?"":path.ToLowerInvariant();}
        static void InitializeFavorites() {
            favorites=new FavoriteLibrary();string file=Path.Combine(directory,"favorites.json");if(!File.Exists(file))return;
            if(new FileInfo(file).Length>16*1024*1024)throw new InvalidOperationException("本地收藏索引过大，请先备份检查。");
            FavoriteLibrary next;
            try{next=JsonConvert.DeserializeObject<FavoriteLibrary>(File.ReadAllText(file,Encoding.UTF8));}catch(JsonException){throw new InvalidOperationException("本地收藏索引损坏，请保留文件并从备份恢复。");}
            if(next==null || next.version!=1 || next.songs==null || next.songs.Count>10000 || next.songs.Any(s=>s==null || string.IsNullOrEmpty(s.id) || FavoriteKey(s).Length==0))throw new InvalidOperationException("本地收藏索引结构无效。");
            if(next.songs.Select(FavoriteKey).Distinct(StringComparer.Ordinal).Count()!=next.songs.Count)throw new InvalidOperationException("本地收藏索引含重复条目。");
            favorites=next;
        }
        public static bool IsFavorite(Song song){string key=FavoriteKey(song);if(key.Length==0)throw new InvalidOperationException("本地歌曲路径无效。");return favorites.songs.Any(s=>FavoriteKey(s)==key);}
        public static void SetFavorite(Song song,bool desired) {
            string key=FavoriteKey(song);if(key.Length==0)throw new InvalidOperationException("本地歌曲路径无效。");
            bool exists=IsFavorite(song);if(exists==desired)return;
            var next=new FavoriteLibrary{songs=favorites.songs.Where(s=>FavoriteKey(s)!=key).ToList()};
            if(desired) {
                if(!File.Exists(Rules.LocalPath(song.path,null)))throw new InvalidOperationException("本地歌曲已移动或删除，请重新导入。");
                next.songs.Add(new Song{id=Hash("favorite|"+key),path=Rules.LocalPath(song.path,null),name=song.name,artists=song.artists==null?new List<string>():song.artists.ToList(),duration=song.duration});
            }
            if(next.songs.Count>10000)throw new InvalidOperationException("本地收藏最多 10000 首。");
            string data=JsonConvert.SerializeObject(next,Formatting.Indented);if(Encoding.UTF8.GetByteCount(data)>16*1024*1024)throw new InvalidOperationException("本地收藏索引达到容量上限。");
            string file=Path.Combine(directory,"favorites.json"),pending=Path.Combine(directory,"favorites.new");
            File.WriteAllText(pending,data,new UTF8Encoding(false));
            if(File.Exists(file))File.Replace(pending,file,Path.Combine(directory,"favorites.bak"));else File.Move(pending,file);
            favorites=next;
        }
    }
}
