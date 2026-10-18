// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol reference: Ximalaya's public web player, /nyx/v2/track/{count,statistic}/web.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChillXimalayaPlaylist
{
    public static partial class Bridge
    {
        static readonly Dictionary<string,string> listeningTokens=new Dictionary<string,string>();
        public static int ReportPlayback(Song song,string playlist,long seconds,long position,long startedAt,long endedAt,int kind)
        {
            long id;
            if(!IsLoggedIn)return 401;
            if(song==null || !long.TryParse(song.id,out id) || id<=0 || seconds<0 || seconds>86400 || position<0 || kind<0 || kind>4)return 400;
            string key=song.id+":"+startedAt.ToString(CultureInfo.InvariantCulture);
            if(kind==0)
            {
                if(listeningTokens.Count>=64)listeningTokens.Clear();
                JObject count=ListeningPost(Origin+"/nyx/v2/track/count/web",Encoding.UTF8.GetBytes(new JObject{{"trackId",id},{"device","www"}}.ToString(Formatting.None)),"application/json",cookies,UserAgent,Origin+"/",XmSign());
                if(Number(count["ret"])!=0 || count["ret"]==null)return 400;
                string token=Text(count["data"]==null?null:count["data"]["token"]);
                if(token.Length==0 || token.Length>1024)return 400;
                listeningTokens[key]=token;return 200;
            }
            string sessionToken;
            if(!listeningTokens.TryGetValue(key,out sessionToken))return 400;
            long album=0; if(playlist!=null && playlist.StartsWith("album:",StringComparison.Ordinal))long.TryParse(playlist.Substring(6),out album);
            if(album<=0)listeningAlbums.TryGetValue(song.id,out album);
            if(album<=0)return 400;
            var body=BuildListeningStatistic(id,album,seconds,position,startedAt,endedAt,sessionToken);
            JObject result=ListeningPost(Origin+"/nyx/v2/track/statistic/web",Encoding.UTF8.GetBytes(body.ToString(Formatting.None)),"application/json",cookies,UserAgent,Origin+"/",XmSign());
            if(kind!=4)listeningTokens.Remove(key);
            return result["ret"]!=null && Number(result["ret"])==0?200:400;
        }
        static readonly Dictionary<string,long> listeningAlbums=new Dictionary<string,long>();
        public static JObject BuildListeningStatistic(long id,long album,long seconds,long position,long start,long end,string token)
        {
            string text="trackId="+id+"&startedAt="+start+"&endedAt="+end+"&duration="+seconds+"&breakSecond="+position;
            string sign;
            using(var aes=Aes.Create())
            {
                aes.Key=ListeningHex("72798eb33fd32602b6ecbd494cbe87fb");aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.PKCS7;
                byte[] bytes=Encoding.UTF8.GetBytes(text);
                using(var encryptor=aes.CreateEncryptor())sign=Convert.ToBase64String(encryptor.TransformFinalBlock(bytes,0,bytes.Length)).TrimEnd('=').Replace('+','-').Replace('/','_');
            }
            return new JObject{{"trackId",id},{"albumId",album},{"startedAt",start},{"endedAt",end},{"duration",seconds},{"listenedDuration",seconds},{"breakSecond",position},{"token",token},{"device","www"},{"sign",sign}};
        }
        static byte[] ListeningHex(string hex)
        {
            var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);return bytes;
        }
    }
}
