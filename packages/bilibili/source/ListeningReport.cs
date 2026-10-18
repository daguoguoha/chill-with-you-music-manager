// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol: https://github.com/SocialSisterYi/bilibili-API-collect/blob/master/docs/video/report.md
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChillBilibiliPlaylist
{
    public static partial class Bridge
    {
        public static int ReportPlayback(Song song,string playlist,long seconds,long position,long startedAt,long endedAt,int kind)
        {
            if(!IsLoggedIn)return 401;
            if(song==null || seconds<0 || position<0 || seconds>86400 || kind<0 || kind>4)return 400;
            Song resolved;
            if(knownSongs.TryGetValue(song.id,out resolved))song=resolved;
            if(!Rules.IsBvid(song.bvid) || song.cid<=0)return 400;
            if(RetryAfterSeconds>0)return 429;
            var fields=new Dictionary<string,string> {
                {"bvid",song.bvid},{"cid",song.cid.ToString(CultureInfo.InvariantCulture)},
                {"played_time",position.ToString(CultureInfo.InvariantCulture)},
                {"realtime",seconds.ToString(CultureInfo.InvariantCulture)},
                {"start_ts",(startedAt/1000).ToString(CultureInfo.InvariantCulture)},
                {"type","3"},{"dt","2"},{"play_type",kind==0?"1":"0"},{"csrf",Text(account["bili_jct"])}
            };
            if(fields["csrf"].Length==0)return 401;
            Throttle();
            string body=string.Join("&",fields.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)).ToArray());
            var result=ListeningPost("https://api.bilibili.com/x/click-interface/web/heartbeat",Encoding.UTF8.GetBytes(body),"application/x-www-form-urlencoded",cookies,UserAgent,"https://www.bilibili.com/video/"+song.bvid,null);
            long code=Number(result["code"]);
            if(result["code"]!=null && code==0)return 200;
            if(code==-101)return 401;
            if(code==-412 || code==-352){restrictedUntil=DateTime.UtcNow.AddSeconds(60);return 429;}
            return 400;
        }
    }
}
