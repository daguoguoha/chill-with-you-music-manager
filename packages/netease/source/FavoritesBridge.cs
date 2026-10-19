// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
namespace ChillNeteasePlaylist {
    public static partial class Bridge {
        public static string FavoriteKey(Song song){return song==null?"":song.id.ToString(CultureInfo.InvariantCulture);}
        public static HashSet<string> GetFavoriteIds(){return FavoritesApi.Read();}
        public static void SetFavorite(Song song,bool desired){FavoritesApi.Set(song,desired);}
    }
    public static partial class FavoritesApi {
        const string Dll="ChillNetease.dll";
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr NeteaseGetLikeList();
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int NeteaseLikeSong(long id,int like);
        public static HashSet<string> Read() {
            var ids=JsonConvert.DeserializeObject<List<long>>(Bridge.Take(NeteaseGetLikeList()));
            if(ids==null)throw new InvalidOperationException("喜欢状态响应为空。");
            var result=new HashSet<string>(StringComparer.Ordinal);foreach(long id in ids)if(id>0)result.Add(id.ToString(CultureInfo.InvariantCulture));return result;
        }
        public static void Set(Song song,bool desired){if(song==null||song.id<=0)throw new InvalidOperationException("歌曲 ID 无效。");if(NeteaseLikeSong(song.id,desired?1:0)!=1)throw new InvalidOperationException("网易云未确认喜欢操作。");}
    }
}
