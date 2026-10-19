// SPDX-License-Identifier: GPL-3.0-or-later
// Endpoints and form fields follow the Ximalaya web player's own collect API.
using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
namespace ChillXimalayaPlaylist {
    public static partial class Bridge {
        public static string FavoriteKey(Song song){long id;return song!=null && long.TryParse(song.id,out id) && id>0?id.ToString(CultureInfo.InvariantCulture):"";}
        static string FavoriteTrack(Song song){string key=FavoriteKey(song);if(key.Length==0)throw new InvalidOperationException("声音 ID 无效。");if(!IsLoggedIn)throw new InvalidOperationException("请先登录喜马拉雅。");return key;}
        public static bool ParseFavoriteState(JObject response) {
            JObject data=response==null?null:response["data"] as JObject;
            JObject info=data==null?null:data["trackInfo"] as JObject;
            JToken liked=info==null?null:info["isLike"];
            if(liked==null || liked.Type!=JTokenType.Boolean)throw new InvalidOperationException("喜马拉雅喜欢状态响应无效。");
            return (bool)liked;
        }
        public static bool IsFavorite(Song song){return ParseFavoriteState(Web("/revision/track/simple?trackId="+FavoriteTrack(song)));}
        public static void SetFavorite(Song song,bool desired) {
            string id=FavoriteTrack(song);
            JObject response=Request(Origin+"/revision/like/"+(desired?"setLikeTrack":"cancelLikeTrack"),cookies,true,false,false,"trackId="+id);
            if(response["ret"]==null || (Number(response["ret"])!=0 && Number(response["ret"])!=200))throw new InvalidOperationException("喜马拉雅未确认喜欢操作。");
        }
    }
}
