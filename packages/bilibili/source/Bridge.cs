// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol references: ChillPatcher.Module.Bilibili, commit 9fce18af841f6eac4f517c1f539f2e97d9cee955.
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
using QRCoder;

namespace ChillBilibiliPlaylist
{
    public static class Bridge
    {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        static string directory, qrKey;
        static JObject account = new JObject();
        static CookieContainer cookies = new CookieContainer(), qrCookies = new CookieContainer();
        static readonly string[] CookieNames = { "SESSDATA", "DedeUserID", "bili_jct", "buvid3", "buvid4" };
        static readonly Uri CookieOrigin = new Uri("https://www.bilibili.com/");
        static readonly Dictionary<string,Song> knownSongs = new Dictionary<string,Song>();
        public static string LastPlaylistTitle { get; private set; }
        static string Text(JToken t) { return t == null || t.Type == JTokenType.Null ? "" : t.ToString(); }
        static long Number(JToken t) { long n; return long.TryParse(Text(t), out n) ? n : 0; }
        public static bool IsLoggedIn { get { return Text(account["SESSDATA"]).Length > 0 && Number(account["DedeUserID"]) > 0; } }
        public static int Init(string path)
        {
            NetworkErrors.EnableModernTls();
            account=new JObject();
            directory=path; Directory.CreateDirectory(path);
            string file=Path.Combine(path,"session.dat");
            if (File.Exists(file))
            {
                try { account=JObject.Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),Encoding.UTF8.GetBytes("ChillBilibili/1"),DataProtectionScope.CurrentUser))); }
                catch { account=new JObject(); }
            }
            cookies=new CookieContainer();
            if (IsLoggedIn) foreach (string name in CookieNames)
            { string value=Text(account[name]); if (value.Length>0) cookies.Add(new Cookie(name,value,"/",".bilibili.com") { Secure=true }); }
            return 0;
        }
        static JObject Request(string url, CookieContainer jar)
        {
            Uri uri=new Uri(url);
            if (uri.Scheme!="https" || (uri.Host!="api.bilibili.com" && uri.Host!="passport.bilibili.com")) throw new InvalidOperationException("接口地址无效。");
            for (int attempt=0; ; attempt++)
            {
                HttpWebRequest req=(HttpWebRequest)WebRequest.Create(uri);
                req.Method="GET"; req.Timeout=15000; req.ReadWriteTimeout=15000; req.AllowAutoRedirect=false;
                req.CookieContainer=jar; req.UserAgent=UserAgent; req.Referer="https://www.bilibili.com/";
                req.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                try
                {
                    using (HttpWebResponse response=(HttpWebResponse)req.GetResponse())
                    using (Stream input=response.GetResponseStream())
                    using (var buffer=new MemoryStream())
                    {
                        if (response.StatusCode!=HttpStatusCode.OK) throw new InvalidOperationException("B站接口 HTTP "+(int)response.StatusCode+"。");
                        byte[] block=new byte[8192]; int n;
                        while ((n=input.Read(block,0,block.Length))>0) { buffer.Write(block,0,n); if (buffer.Length>16*1024*1024) throw new InvalidOperationException("B站响应过大。"); }
                        JObject result;
                        try { result=JObject.Parse(Encoding.UTF8.GetString(buffer.ToArray())); }
                        catch (JsonException) { throw new InvalidOperationException("B站接口响应无效，请换网络或稍后重试。"); }
                        if (result["code"]==null) throw new InvalidOperationException("B站接口结构已变化。");
                        long code=Number(result["code"]);
                        if (code!=0)
                        {
                            string hint=code==-101 ? "登录已过期，请重新扫码。" : code==-412 || code==-352 ? "请求暂被限制，请稍后再试或换网络。" : code==-403 || code==-404 || code==62002 ? "内容不可访问，请检查账号权限或视频是否失效。" : "请稍后重试或检查内容权限。";
                            throw new InvalidOperationException("B站接口 "+code+"："+hint);
                        }
                        return result;
                    }
                }
                catch (WebException error)
                {
                    bool retry=attempt==0 && NetworkErrors.CanRetry(error);
                    string message=NetworkErrors.Describe(error,uri.Host);
                    if (error.Response!=null) error.Response.Close();
                    if (retry) continue;
                    throw new InvalidOperationException(message);
                }
            }
        }
        static JObject Api(string path) { return Request("https://api.bilibili.com"+path,cookies); }
        public static void CancelQr() { qrKey=null; qrCookies=new CookieContainer(); }
        public static string QrImage(string ignored)
        {
            CancelQr();
            JObject result=Request("https://passport.bilibili.com/x/passport-login/web/qrcode/generate",qrCookies);
            JObject data=result["data"] as JObject;
            string value=data==null ? "" : Text(data["url"]);
            Uri uri;
            if (!Uri.TryCreate(value,UriKind.Absolute,out uri) || uri.Scheme!="https" || (uri.Host!="passport.bilibili.com" && uri.Host!="account.bilibili.com")) throw new InvalidOperationException("B站未返回有效登录二维码。");
            qrKey=Text(data["qrcode_key"]);
            if (qrKey.Length<10) throw new InvalidOperationException("B站二维码缺少凭证。");
            using (var generator=new QRCodeGenerator())
            using (var qrData=generator.CreateQrCode(value,QRCodeGenerator.ECCLevel.M))
            using (var png=new PngByteQRCode(qrData)) return Convert.ToBase64String(png.GetGraphic(7));
        }
        public static string CheckQr()
        {
            if (string.IsNullOrEmpty(qrKey)) return JsonConvert.SerializeObject(new QrState { code=65 });
            JObject r=Request("https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key="+Uri.EscapeDataString(qrKey),qrCookies);
            JObject data=r["data"] as JObject;
            if (data==null || data["code"]==null) throw new InvalidOperationException("B站登录响应无效。");
            long state=Number(data["code"]);
            if (state==0)
            {
                JObject next=new JObject();
                CookieCollection available=qrCookies.GetCookies(CookieOrigin);
                foreach (string name in CookieNames) { Cookie item=available[name]; if (item!=null && !item.Expired) next[name]=item.Value; }
                if (Text(next["SESSDATA"]).Length==0 || Number(next["DedeUserID"])<=0) throw new InvalidOperationException("扫码未返回完整登录信息，请重新扫码。");
                byte[] encrypted=ProtectedData.Protect(Encoding.UTF8.GetBytes(next.ToString(Formatting.None)),Encoding.UTF8.GetBytes("ChillBilibili/1"),DataProtectionScope.CurrentUser);
                string pending=Path.Combine(directory,"session.new"),target=Path.Combine(directory,"session.dat");
                File.WriteAllBytes(pending,encrypted);
                if (File.Exists(target)) File.Replace(pending,target,null); else File.Move(pending,target);
                account=next; cookies=qrCookies; qrKey=null;
            }
            return JsonConvert.SerializeObject(new QrState { code=state==0 ? 0 : state==86101 ? 66 : state==86090 ? 67 : 65 });
        }
        public static string UserInfo()
        {
            JObject r=Api("/x/web-interface/nav");
            if (r["data"]==null || r["data"]["isLogin"]==null || !(bool)r["data"]["isLogin"]) throw new InvalidOperationException("B站登录已过期，请重新扫码。");
            return JsonConvert.SerializeObject(new User { nickname=Text(r["data"]["uname"]) });
        }
        public static string UserPlaylists()
        {
            if (!IsLoggedIn) throw new InvalidOperationException("请先用 B站 App 扫码登录。");
            JObject r=Api("/x/v3/fav/folder/created/list-all?up_mid="+Text(account["DedeUserID"])+"&type=2");
            JToken data=r["data"]; JArray rows=data==null ? null : data["list"] as JArray;
            if (rows==null && data!=null && data["count"]!=null && data["count"].Type==JTokenType.Integer && Number(data["count"])==0) rows=new JArray();
            if (rows==null) throw new InvalidOperationException("B站收藏夹列表结构已变化。");
            var lists=new List<Playlist>();
            foreach (JObject row in rows.OfType<JObject>()) if (Number(row["id"])>0)
                lists.Add(new Playlist { id="fav:"+Text(row["id"]),name=Text(row["title"]),songCount=(int)Number(row["media_count"]) });
            return JsonConvert.SerializeObject(new UserPlaylists { created=lists });
        }
        static string ResolveShortLink(string value)
        {
            string current=value.Substring(6);
            for (int hop=0;hop<5;hop++)
            {
                string parsed=Rules.PlaylistId(current);
                if (parsed==null) throw new InvalidOperationException("短链接跳转到了不支持的地址。");
                if (!parsed.StartsWith("short:",StringComparison.Ordinal)) return parsed;
                HttpWebRequest req=(HttpWebRequest)WebRequest.Create(current);
                req.Method="GET"; req.Timeout=15000; req.ReadWriteTimeout=15000; req.AllowAutoRedirect=false; req.UserAgent=UserAgent;
                try
                {
                    using (HttpWebResponse response=(HttpWebResponse)req.GetResponse())
                    {
                        int status=(int)response.StatusCode; string location=response.Headers["Location"];
                        if (status<300 || status>=400 || string.IsNullOrEmpty(location)) throw new InvalidOperationException("短链接没有返回视频或收藏夹，请复制完整链接。");
                        current=new Uri(new Uri(current),location).AbsoluteUri;
                    }
                }
                catch (WebException error)
                { string message=NetworkErrors.Describe(error,"b23.tv"); if (error.Response!=null) error.Response.Close(); throw new InvalidOperationException(message); }
            }
            throw new InvalidOperationException("短链接跳转次数过多，请复制完整链接。");
        }
        static Song VideoSong(JObject data,JObject page,bool multi)
        {
            string bv=Text(data["bvid"]); long cid=Number(page["cid"]);
            if (!Rules.IsBvid(bv) || cid<=0) throw new InvalidOperationException("视频信息缺少有效 BV 号或分 P。");
            string title=Text(data["title"]);
            if (multi) title+=" · P"+Number(page["page"])+" "+Text(page["part"]);
            return new Song { id=bv+":"+cid,bvid=bv,cid=cid,name=title,duration=Number(page["duration"]),artists=new List<string> { Text(data["owner"]==null ? null : data["owner"]["name"]) } };
        }
        public static List<Song> Playlist(string input)
        {
            string id=Rules.PlaylistId(input);
            if (id==null) throw new InvalidOperationException("视频或收藏夹链接无效。");
            if (id.StartsWith("short:",StringComparison.Ordinal)) id=ResolveShortLink(id);
            var songs=new List<Song>();
            string title="导入的视频 / 收藏夹";
            if (id.StartsWith("video:",StringComparison.Ordinal))
            {
                string[] bits=id.Split(':');
                JObject data=Api("/x/web-interface/view?bvid="+bits[1])["data"] as JObject;
                JArray pages=data==null ? null : data["pages"] as JArray;
                if (pages==null || pages.Count==0) throw new InvalidOperationException("视频没有可用分 P。");
                if (pages.Count>10000) throw new InvalidOperationException("视频分 P 超过上限。");
                title=Text(data["title"]);
                foreach (JObject page in pages.OfType<JObject>())
                    if (bits[2]=="all" || Text(page["page"])==bits[2]) songs.Add(VideoSong(data,page,pages.Count>1));
                if (songs.Count==0) throw new InvalidOperationException("指定的分 P 不存在。");
            }
            else
            {
                bool complete=false; var seen=new HashSet<string>(); int received=0; long total=-1;
                for (int page=1;page<=500;page++)
                {
                    JObject r=Api("/x/v3/fav/resource/list?media_id="+id.Substring(4)+"&ps=20&pn="+page);
                    JObject data=r["data"] as JObject;
                    JArray rows=data==null ? null : data["medias"] as JArray;
                    if (rows==null && data!=null && data["info"]!=null && data["info"]["media_count"]!=null && data["info"]["media_count"].Type==JTokenType.Integer && Number(data["info"]["media_count"])==0) rows=new JArray();
                    if (rows==null) throw new InvalidOperationException("收藏夹不可访问或列表不完整，请检查权限。");
                    long reported=data["info"]==null ? -1 : Number(data["info"]["media_count"]);
                    if (page==1) { total=reported; if (data["info"]!=null) title=Text(data["info"]["title"]); }
                    else if (reported>=0 && total>=0 && reported!=total) throw new InvalidOperationException("收藏夹在读取期间发生变化，请重新读取。");
                    received+=rows.Count;
                    foreach (JObject row in rows.OfType<JObject>())
                    {
                        string bv=Text(row["bvid"]);
                        if (!Rules.IsBvid(bv) || Text(row["title"])=="已失效视频" || !seen.Add(bv)) continue;
                        songs.Add(new Song { id=bv+":0",bvid=bv,cid=0,name=Text(row["title"]),duration=Number(row["duration"]),artists=new List<string> { Text(row["upper"]==null ? null : row["upper"]["name"]) } });
                    }
                    if (data["has_more"]==null) throw new InvalidOperationException("收藏夹分页结构已变化。");
                    bool more=(bool)data["has_more"];
                    // Folder totals can include deleted/private videos omitted by this API.
                    // The server's has_more flag determines completion for accessible entries.
                    if (!more) { complete=true; break; }
                    if (rows.Count==0) throw new InvalidOperationException("收藏夹分页中断，请重新读取。");
                    Thread.Sleep(350);
                }
                if (!complete) throw new InvalidOperationException("收藏夹超过 10000 条，请缩小范围。");
            }
            foreach (Song song in songs) knownSongs[song.id]=song;
            LastPlaylistTitle=string.IsNullOrWhiteSpace(title) ? "导入的视频 / 收藏夹" : title;
            return songs;
        }
        public static string SongURL(string id)
        {
            Song song;
            if (!knownSongs.TryGetValue(id,out song)) throw new InvalidOperationException("请重新读取视频列表。");
            if (song.cid<=0)
            {
                JObject view=Api("/x/web-interface/view?bvid="+song.bvid)["data"] as JObject;
                JArray pages=view==null ? null : view["pages"] as JArray;
                if (pages==null || pages.Count==0) throw new InvalidOperationException("视频已失效或没有可用分 P。");
                song.cid=Number(pages[0]["cid"]); song.duration=Number(pages[0]["duration"]);
            }
            if (song.cid<=0) throw new InvalidOperationException("视频分 P 无效。");
            JObject data=Api("/x/player/playurl?bvid="+song.bvid+"&cid="+song.cid.ToString(CultureInfo.InvariantCulture)+"&fnval=16&fnver=0&fourk=0")["data"] as JObject;
            if (data==null || Number(data["is_drm"])!=0 || Text(data["is_drm"])=="True" || Number(data["is_preview"])!=0 || Text(data["is_preview"])=="True") throw new InvalidOperationException("视频受保护或仅返回预览，请检查账号权限。");
            JObject dash=data["dash"] as JObject; JArray audio=dash==null ? null : dash["audio"] as JArray;
            if (audio==null) throw new InvalidOperationException("视频没有当前账号可播放的独立音频。");
            double length=Number(data["timelength"])/1000.0;
            if (song.duration>30 && length>0 && length+8<song.duration) throw new InvalidOperationException("仅返回部分音频，请检查账号权限。");
            foreach (JObject stream in audio.OfType<JObject>().Where(a=>Text(a["codecs"]).StartsWith("mp4a.",StringComparison.Ordinal)).OrderByDescending(a=>Number(a["bandwidth"])))
            {
                var candidates=new List<string> { Text(stream["baseUrl"] ?? stream["base_url"]) };
                JArray backup=stream["backupUrl"] as JArray ?? stream["backup_url"] as JArray;
                if (backup!=null) candidates.AddRange(backup.Select(Text));
                string url=candidates.FirstOrDefault(Rules.IsMusicUrl);
                if (url!=null) return JsonConvert.SerializeObject(new SongUrl { url=Rules.SecureMusicUrl(url),type="m4a",size=Number(stream["size"]) });
            }
            throw new InvalidOperationException("视频没有可用 AAC 音频，请稍后重试。");
        }
    }
}
