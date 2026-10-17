// SPDX-License-Identifier: GPL-3.0-or-later
// API/signature/URL decoding reference: jerryqx/dsh-ximalaya, MIT.
// Copyright (c) 2026 dsh-ximalaya contributors. See licenses/dsh-ximalaya-LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChillXimalayaPlaylist
{
    public static class Bridge
    {
        public const string UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
        const string Origin="https://www.ximalaya.com";
        static string directory, qrKey;
        static DateTime qrExpires;
        static CookieContainer cookies=new CookieContainer(), qrCookies=new CookieContainer();
        static JObject account=new JObject();
        static readonly string[] CookieNames={"1&_token","1_l_flag","1&remember_me"};
        static readonly Dictionary<string,Song> knownSongs=new Dictionary<string,Song>();
        public static string LastPlaylistTitle {get;private set;}
        static string Text(JToken t) {return t==null || t.Type==JTokenType.Null ? "" : t.ToString();}
        static long Number(JToken t) {long n;return long.TryParse(Text(t),out n)?n:0;}
        static bool Flag(JToken t) {return t!=null && (t.Type==JTokenType.Boolean ? (bool)t : Text(t)=="1");}
        static long Now {get{return (long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalMilliseconds;}}
        public static bool IsLoggedIn {get{return Text(account["1&_token"]).Length>0;}}
        public static int Init(string path)
        {
            NetworkErrors.EnableModernTls(); directory=path; Directory.CreateDirectory(path);
            account=new JObject();cookies=new CookieContainer();knownSongs.Clear();CancelQr();
            string file=Path.Combine(path,"session.dat");
            if(File.Exists(file)) try{account=JObject.Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),Encoding.UTF8.GetBytes("ChillXimalaya/1"),DataProtectionScope.CurrentUser)));}catch{account=new JObject();}
            if(IsLoggedIn) foreach(string name in CookieNames)
            {string value=Text(account[name]);if(value.Length>0)cookies.Add(new Cookie(name,value,"/",".ximalaya.com"){Secure=true});}
            return 0;
        }
        static string ApiError(long code)
        {
            return "喜马拉雅接口 "+code+"："+(code==401 || code==50 ? "登录已过期，请重新扫码。" : code==1001 || code==407 ? "请求暂被限制，请稍后重试。" : code==-3 || code==404 ? "内容已下架或不可访问。" : "请稍后重试或检查账号访问权限。");
        }
        static JObject Request(string url,CookieContainer jar,bool signed,bool raw,bool ting)
        {
            Uri uri=new Uri(url);
            if(uri.Scheme!="https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !new[]{"www.ximalaya.com","m.ximalaya.com","mobile.ximalaya.com","passport.ximalaya.com"}.Contains(uri.Host))throw new InvalidOperationException("接口地址无效。");
            for(int attempt=0;;attempt++)
            {
                var req=(HttpWebRequest)WebRequest.Create(uri);
                req.Method="GET";req.Timeout=15000;req.ReadWriteTimeout=15000;req.AllowAutoRedirect=false;
                req.CookieContainer=jar;req.UserAgent=ting ? "ting_6.7.9(GM1900,Android29)" : UserAgent;req.Referer=Origin+"/";
                req.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                if(signed)req.Headers["xm-sign"]=XmSign();
                try
                {
                    using(var response=(HttpWebResponse)req.GetResponse())
                    using(var input=response.GetResponseStream())
                    using(var output=new MemoryStream())
                    {
                        if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidOperationException("喜马拉雅接口 HTTP "+(int)response.StatusCode+"。");
                        byte[] block=new byte[8192];int n;
                        while((n=input.Read(block,0,block.Length))>0){output.Write(block,0,n);if(output.Length>16*1024*1024)throw new InvalidOperationException("接口响应过大。");}
                        JObject result;
                        try{result=JObject.Parse(Encoding.UTF8.GetString(output.ToArray()));}catch(JsonException){throw new InvalidOperationException("喜马拉雅接口响应无效，请稍后重试。");}
                        if(!raw && result["ret"]!=null && Number(result["ret"])!=0 && Number(result["ret"])!=200)throw new InvalidOperationException(ApiError(Number(result["ret"])));
                        return result;
                    }
                }
                catch(WebException error)
                {
                    bool retry=attempt==0 && NetworkErrors.CanRetry(error);string message=NetworkErrors.Describe(error,uri.Host);
                    if(error.Response!=null)error.Response.Close();if(retry)continue;throw new InvalidOperationException(message);
                }
            }
        }
        static JObject Web(string path) {return Request(Origin+path,cookies,true,false,false);}
        static JObject NeedData(JObject result) {JObject data=result["data"] as JObject;if(data==null)throw new InvalidOperationException("喜马拉雅接口结构已变化。");return data;}
        public static void CancelQr() {qrKey=null;qrCookies=new CookieContainer();}
        public static string QrImage(string ignored)
        {
            CancelQr();JObject r=Request("https://passport.ximalaya.com/web/qrCode/gen?level=L&source=ximalaya-web",qrCookies,false,false,false);
            string key=Text(r["qrId"]),img=Text(r["img"]);byte[] png;
            try{png=Convert.FromBase64String(img);}catch(FormatException){throw new InvalidOperationException("登录二维码图片无效。");}
            if(key.Length<5 || key.Length>512 || png.Length<8 || png.Length>1024*1024 || png[0]!=137 || png[1]!=80)throw new InvalidOperationException("登录二维码数据无效。");
            qrKey=key;qrExpires=DateTime.UtcNow.AddMinutes(3);return img;
        }
        public static string CheckQr()
        {
            if(string.IsNullOrEmpty(qrKey) || DateTime.UtcNow>=qrExpires){CancelQr();return JsonConvert.SerializeObject(new QrState{code=65});}
            JObject r=Request("https://passport.ximalaya.com/web/qrCode/check/"+Uri.EscapeDataString(qrKey)+"/"+Now,qrCookies,false,true,false);
            long state=Number(r["ret"]);
            if(r["ret"]==null)throw new InvalidOperationException("喜马拉雅登录响应无效。");
            if(state==0)
            {
                var next=new JObject();CookieCollection available=qrCookies.GetCookies(new Uri(Origin));
                foreach(string name in CookieNames){Cookie item=available[name];if(item!=null && !item.Expired)next[name]=item.Value;}
                if(Text(next["1&_token"]).Length==0)return JsonConvert.SerializeObject(new QrState{code=66});
                // Validate the session before replacing an existing account.
                JObject user=NeedData(Request(Origin+"/revision/main/getCurrentUser",qrCookies,true,false,false));
                if(Number(user["uid"])<=0)throw new InvalidOperationException("登录未返回有效账号，请重新扫码。");
                byte[] encrypted=ProtectedData.Protect(Encoding.UTF8.GetBytes(next.ToString(Formatting.None)),Encoding.UTF8.GetBytes("ChillXimalaya/1"),DataProtectionScope.CurrentUser);
                string pending=Path.Combine(directory,"session.new"),target=Path.Combine(directory,"session.dat");
                File.WriteAllBytes(pending,encrypted);if(File.Exists(target))File.Replace(pending,target,null);else File.Move(pending,target);
                account=next;cookies=qrCookies;qrKey=null;return JsonConvert.SerializeObject(new QrState{code=0});
            }
            // Non-success states carry no credentials; local expiry bounds polling.
            if(state==1001 || state==407)throw new InvalidOperationException(ApiError(state));
            return JsonConvert.SerializeObject(new QrState{code=66});
        }
        public static string UserInfo()
        {
            if(!IsLoggedIn)throw new InvalidOperationException("请先扫码登录。");
            JObject data=NeedData(Web("/revision/main/getCurrentUser"));
            if(Number(data["uid"])<=0)throw new InvalidOperationException("喜马拉雅登录已过期，请重新扫码。");
            return JsonConvert.SerializeObject(new User{nickname=Text(data["nickname"])});
        }
        public static string UserPlaylists()
        {
            if(!IsLoggedIn)throw new InvalidOperationException("请先用喜马拉雅 App 扫码登录。");
            var result=new List<Playlist>{new Playlist{id="likes",name="喜欢的声音",songCount=0}};var seen=new HashSet<string>();
            bool complete=false;
            for(int page=1;page<=200;page++)
            {
                JObject data=NeedData(Web("/revision/my/getSubscriptionSynthesize?pageNum="+page+"&pageSize=50"));
                JArray rows=data["albumsInfo"] as JArray;if(rows==null)throw new InvalidOperationException("订阅列表结构已变化。");
                int added=0;
                foreach(JObject row in rows.OfType<JObject>())
                {long id=Number(row["id"]);if(id>0 && seen.Add("album:"+id)){result.Add(new Playlist{id="album:"+id,name=Text(row["title"]),songCount=(int)Math.Min(int.MaxValue,Number(row["trackCount"]))});added++;}}
                if(rows.Count==0 || (data["totalCount"]!=null && page*50>=Number(data["totalCount"])) || (data["totalCount"]==null && rows.Count<50)){complete=true;break;}
                if(added==0)throw new InvalidOperationException("订阅分页重复，请稍后重试。");Thread.Sleep(200);
            }
            if(!complete)throw new InvalidOperationException("订阅超过 10000 个，请直接导入目标专辑。");
            return JsonConvert.SerializeObject(new UserPlaylists{created=result});
        }
        static string ResolveShort(string id)
        {
            string current=id.Substring(6);
            for(int hop=0;hop<5;hop++)
            {
                string parsed=Rules.PlaylistId(current);if(parsed==null)throw new InvalidOperationException("分享链接跳转到不支持的地址，请复制完整专辑链接。");
                if(!parsed.StartsWith("short:",StringComparison.Ordinal))return parsed;
                var req=(HttpWebRequest)WebRequest.Create(current);req.Timeout=15000;req.ReadWriteTimeout=15000;req.AllowAutoRedirect=false;req.UserAgent=UserAgent;
                try{using(var r=(HttpWebResponse)req.GetResponse()){int status=(int)r.StatusCode;string location=r.Headers["Location"];if(status<300 || status>=400 || string.IsNullOrEmpty(location))throw new InvalidOperationException("分享链接无法解析，请复制完整专辑或声音链接。");current=new Uri(new Uri(current),location).AbsoluteUri;}}
                catch(WebException e){string message=NetworkErrors.Describe(e,new Uri(current).Host);if(e.Response!=null)e.Response.Close();throw new InvalidOperationException(message);}
            }
            throw new InvalidOperationException("分享链接跳转过多，请复制完整链接。");
        }
        static Song Track(JObject row)
        {
            long id=Number(row["trackId"] ?? row["id"]);
            return id<=0 || Flag(row["isVideo"]) ? null : new Song{id=id.ToString(CultureInfo.InvariantCulture),name=Text(row["title"] ?? row["trackTitle"] ?? row["trackName"]),duration=Number(row["duration"] ?? row["length"] ?? row["trackLength"]),artists=new List<string>{Text(row["nickname"] ?? row["anchorName"] ?? row["anchorNickName"])}};
        }
        public static List<Song> Playlist(string input)
        {
            string id=Rules.PlaylistId(input);if(id==null)throw new InvalidOperationException("请输入喜马拉雅专辑或声音链接。");
            if(id.StartsWith("short:",StringComparison.Ordinal))id=ResolveShort(id);
            var result=new List<Song>();var seen=new HashSet<string>();string title="导入的专辑";
            if(id.StartsWith("track:",StringComparison.Ordinal))
            {
                JObject row=Request("https://m.ximalaya.com/tracks/"+id.Substring(6)+".json",cookies,false,false,false);
                Song song=Track(row);if(song==null)throw new InvalidOperationException("声音不存在或不支持。");
                result.Add(song);title=song.name;
            }
            else
            {
                bool likes=id=="likes",complete=false;string album=likes?null:id.Substring(6);
                if(likes){if(!IsLoggedIn)throw new InvalidOperationException("请先扫码登录。");title="喜欢的声音";}
                else
                {
                    JObject data=NeedData(Web("/revision/album/v1/simple?albumId="+album));
                    JObject info=data["albumPageMainInfo"] as JObject;
                    if(info==null)throw new InvalidOperationException("专辑信息结构已变化。");title=Text(info["albumTitle"]);
                }
                bool useWeb=false;
                for(int page=1;page<=200;page++)
                {
                    JObject data;JArray rows;bool more;
                    if(likes)
                    {
                        data=NeedData(Web("/revision/my/getLikeTracks?pageNum="+page+"&pageSize=50"));rows=data["tracksList"] as JArray;
                        more=data["hasMore"]!=null ? Flag(data["hasMore"]) : page*50<Number(data["totalCount"]);
                    }
                    else
                    {
                        data=null;rows=null;more=false;
                        if(!useWeb)
                        {
                            try{data=NeedData(Request("https://mobile.ximalaya.com/mobile/v1/album/track/?albumId="+album+"&pageSize=50&pageId="+page,cookies,false,false,false));rows=data["list"] as JArray;more=page<Number(data["maxPageId"]);}
                            catch(InvalidOperationException){if(page!=1)throw;useWeb=true;}
                            if(page==1 && (rows==null || rows.Count==0))useWeb=true;
                        }
                        if(useWeb)
                        {data=NeedData(Web("/revision/play/v1/show?id="+album+"&num="+page+"&sort=0&size=50&ptype=0"));rows=data["tracksAudioPlay"] as JArray;if(data["hasMore"]==null)throw new InvalidOperationException("专辑分页结构已变化。");more=Flag(data["hasMore"]);}
                    }
                    if(rows==null)throw new InvalidOperationException("声音列表结构已变化。");
                    int added=0;
                    foreach(JObject row in rows.OfType<JObject>()){Song song=Track(row);if(song!=null && seen.Add(song.id)){result.Add(song);added++;}}
                    if(!more){complete=true;break;}
                    if(rows.Count==0 || added==0)throw new InvalidOperationException("声音分页未完成，请稍后重试。");Thread.Sleep(200);
                }
                if(!complete)throw new InvalidOperationException("专辑超过 10000 集，请单独导入声音链接。");
            }
            foreach(Song song in result){if(string.IsNullOrEmpty(song.name))song.name="未命名声音";knownSongs[song.id]=song;}
            LastPlaylistTitle=string.IsNullOrEmpty(title)?"导入的专辑":title;return result;
        }
        public static string SongURL(string id)
        {
            long number;if(!long.TryParse(id,out number) || number<=0)throw new InvalidOperationException("声音 ID 无效。");
            string url=null,type=null;double duration=0;string failure=null;
            if(IsLoggedIn)
            {
                try
                {
                    JObject r=Request(Origin+"/mobile-playpage/track/v3/baseInfo/"+Now+"?device=www2&trackQualityLevel=2&trackId="+number,cookies,true,false,true);
                    JObject info=r["trackInfo"] as JObject;
                    if(info==null)throw new InvalidOperationException("声音播放信息结构已变化。");
                    if(!Flag(info["isAuthorized"]))throw new InvalidOperationException("当前账号没有此声音的完整播放权限。");
                    JArray list=info["playUrlList"] as JArray;
                    if(list!=null)foreach(string quality in new[]{"M4A_128","MP3_64","MP3_32"})
                    {
                        JObject item=list.OfType<JObject>().FirstOrDefault(v=>Text(v["type"])==quality);
                        if(item==null)continue;string value=Text(item["url"]);string candidate=Rules.IsMusicUrl(value)?value:DecodeUrl(value);
                        candidate=Rules.SecureMusicUrl(candidate);if(candidate==null)continue;
                        url=candidate;type=quality.StartsWith("M4A",StringComparison.Ordinal)?"m4a":"mp3";duration=Number(info["duration"]);break;
                    }
                }
                catch(InvalidOperationException error){failure=error.Message;}
            }
            if(url==null)
            {
                JObject r=Request("https://m.ximalaya.com/tracks/"+number+".json",cookies,false,false,false);
                // Never treat a paid preview as the complete episode.
                bool restricted=Flag(r["is_paid"]) || Flag(r["isVipFirst"]);
                if(restricted && !Flag(r["is_free"]))throw new InvalidOperationException(failure ?? "此声音需要登录并拥有已购或会员播放权限。");
                foreach(string key in new[]{"play_path_64","play_path_32","play_path"})
                {url=Rules.SecureMusicUrl(Text(r[key]));if(url!=null)break;}
                if(url==null)throw new InvalidOperationException(failure ?? "喜马拉雅未提供可播放音源，请检查账号权限或稍后重试。");
                string ext=Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();type=ext==".mp3"?"mp3":ext==".aac"?"aac":"m4a";duration=Number(r["duration"]);
            }
            Song song;if(duration>0 && knownSongs.TryGetValue(id,out song))song.duration=duration;
            return JsonConvert.SerializeObject(new SongUrl{url=url,type=type,size=0});
        }
        // Ordinary web-client request signature. No challenge solving or altered entitlement.
        public static string XmSign()
        {
            byte[] random=new byte[6];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);
            string head=BitConverter.ToString(random).Replace("-","").ToLowerInvariant()+Base62(Now)+"0202";
            uint crc=0xffffffff;foreach(byte b in Encoding.UTF8.GetBytes(head)){crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0 ? (crc>>1)^0xedb88320 : crc>>1;}crc^=0xffffffff;
            byte[] data=Encoding.UTF8.GetBytes(head+crc.ToString("x8")),key=Encoding.ASCII.GetBytes("y3hbnr8d4s2ztjbc");
            using(var aes=Aes.Create()){aes.Key=key;aes.IV=key;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;using(var transform=aes.CreateEncryptor()){byte[] cipher=transform.TransformFinalBlock(data,0,data.Length);return "&&"+Convert.ToBase64String(cipher).TrimEnd('=').Replace('+','-').Replace('/','_')+"_2";}}
        }
        static string Base62(long number){const string digits="0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";string value="";do{value=digits[(int)(number%62)]+value;number/=62;}while(number>0);return value.PadLeft(8,'0');}
        static readonly byte[] SBox={183,174,108,16,131,159,250,5,239,110,193,202,153,137,251,176,119,150,47,204,97,237,1,71,177,42,88,218,166,82,87,94,14,195,69,127,215,240,225,197,238,142,123,44,219,50,190,29,181,186,169,98,139,185,152,13,141,76,6,157,200,132,182,49,20,116,136,43,155,194,101,231,162,242,151,213,53,60,26,134,211,56,28,223,107,161,199,15,229,61,96,41,66,158,254,21,165,253,103,89,3,168,40,246,81,95,58,31,172,78,99,45,148,187,222,124,55,203,235,64,68,149,180,35,113,207,118,111,91,38,247,214,7,212,209,189,241,18,115,173,25,236,121,249,75,57,216,10,175,112,234,164,70,206,198,255,140,230,12,32,83,46,245,0,62,227,72,191,156,138,248,114,220,90,84,170,128,19,24,122,146,80,39,37,8,34,22,11,93,130,63,154,244,160,144,79,23,133,92,54,102,210,65,67,27,196,201,106,143,52,74,100,217,179,48,233,126,117,184,226,85,171,167,86,2,147,17,135,228,252,105,30,192,129,178,120,36,145,51,163,77,205,73,4,188,125,232,33,243,109,224,104,208,221,59,9};
        static readonly byte[] UrlKey={204,53,135,197,39,73,58,160,79,24,12,83,180,250,101,60,206,30,10,227,36,95,161,16,135,150,235,116,242,116,165,171};
        // Decode the URL envelope returned after server-side permission validation.
        public static string DecodeUrl(string value)
        {
            if(string.IsNullOrEmpty(value) || value.Length>16384)return null;
            try
            {
                string encoded=value.Replace('_','/').Replace('-','+');encoded=encoded.PadRight((encoded.Length+3)/4*4,'=');byte[] all=Convert.FromBase64String(encoded);
                if(all.Length<17)return null;int length=all.Length-16;byte[] output=new byte[length];
                for(int i=0;i<length;i++)output[i]=(byte)(SBox[all[i]]^all[length+i%16]^UrlKey[i%32]);
                return new UTF8Encoding(false,true).GetString(output);
            }
            catch(FormatException){return null;}catch(DecoderFallbackException){return null;}
        }
    }
}
