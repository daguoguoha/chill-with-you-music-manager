// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Newtonsoft.Json.Linq;
namespace ChillBilibiliPlaylist {
    public static partial class Bridge {
        public static string FavoriteKey(Song song){return song!=null && Rules.IsBvid(song.bvid)?song.bvid:"";}
        static string FavoriteVideo(Song song){string key=FavoriteKey(song);if(key.Length==0)throw new InvalidOperationException("视频 BV 号无效。");if(!IsLoggedIn)throw new InvalidOperationException("请先登录B站。");return key;}
        public static bool ParseFavoriteState(JObject response) {
            JObject data=response==null?null:response["data"] as JObject;
            JToken liked=data==null?null:data["like"];
            if(liked==null || liked.Type!=JTokenType.Boolean)throw new InvalidOperationException("B站点赞状态响应无效。");
            return (bool)liked;
        }
        public static bool IsFavorite(Song song){return ParseFavoriteState(Api("/x/web-interface/archive/relation?bvid="+Uri.EscapeDataString(FavoriteVideo(song))));}
        public static void SetFavorite(Song song,bool desired) {
            string bv=FavoriteVideo(song);PrepareDeviceCookies();
            string csrf=Text(account["bili_jct"]);
            if(string.IsNullOrEmpty(csrf))throw new InvalidOperationException("登录缺少操作凭证，请重新扫码。");
            Request("https://api.bilibili.com/x/web-interface/archive/like",cookies,"bvid="+Uri.EscapeDataString(bv)+"&like="+(desired?"1":"2")+"&csrf="+Uri.EscapeDataString(csrf));
        }
    }
}
