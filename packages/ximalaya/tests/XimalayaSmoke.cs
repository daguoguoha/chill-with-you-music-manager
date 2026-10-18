using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChillXimalayaPlaylist;
class XimalayaSmoke
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int Main(string[] args)
    {
        try
        {
            if(args.Length>0 && args[0]=="rules")
            {
                Check(Rules.PlaylistId("2677356")=="album:2677356","album ID");
                Check(Rules.PlaylistId("https://www.ximalaya.com/album/2677356")=="album:2677356","album link");
                Check(Rules.PlaylistId("https://m.ximalaya.com/sound/25202")=="track:25202","sound link");
                Check(Rules.PlaylistId("https://www.ximalaya.com/youshengshu/2677356/25202")=="track:25202","legacy sound link");
                Check(Rules.PlaylistId("https://www.ximalaya.com/share/album/2677356")=="album:2677356","share link");
                Check(Rules.PlaylistId("track:25202")=="track:25202","explicit sound ID");
                Check(Rules.PlaylistId("https://xima.tv/test").StartsWith("short:"),"constrained short link");
                foreach(string url in new[]{"https://evil.test/album/1","https://www.ximalaya.com.evil.test/album/1","https://me:pass@www.ximalaya.com/album/1","https://www.ximalaya.com:9999/album/1","http://127.0.0.1/album/1","album:-1","album:0","track:922337203685477580700"})Check(Rules.PlaylistId(url)==null,"invalid import rejected");
                foreach(string url in new[]{"https://evil.test/a","https://a.xmcdn.com.evil.test/a","https://me:pass@a.xmcdn.com/a","https://a.xmcdn.com:9999/a","https://127.0.0.1/a"})Check(!Rules.IsMusicUrl(url),"foreign media rejected");
                Check(Rules.SecureMusicUrl("http://a.xmcdn.com/test.m4a")=="https://a.xmcdn.com/test.m4a","HTTPS media upgrade");
                Check(Bridge.XmSign().StartsWith("&&") && Bridge.XmSign().EndsWith("_2"),"request signature envelope");
                Check(Bridge.DecodeUrl("bad-input")==null,"invalid encoded URL rejected");
                return 0;
            }
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-account");
            if(args.Length==2 && args[0]=="session")
            {
                Bridge.Init(args[1]);Check(Bridge.IsLoggedIn,"protected session reload");
                var user=JsonConvert.DeserializeObject<User>(Bridge.UserInfo());Check(!string.IsNullOrEmpty(user.nickname),"session valid");
                var lists=Rules.MergePlaylists(JsonConvert.DeserializeObject<UserPlaylists>(Bridge.UserPlaylists()));
                Check(lists.Any(x=>x.id=="likes"),"likes list available");Console.WriteLine("Subscriptions="+(lists.Count-1));
                Check(Bridge.Playlist("likes")!=null,"likes pagination");
                var method=typeof(Bridge).GetMethod("Request",BindingFlags.NonPublic|BindingFlags.Static);
                var jar=typeof(Bridge).GetField("cookies",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                long timestamp=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalMilliseconds;
                var response=(JObject)method.Invoke(null,new object[]{"https://www.ximalaya.com/mobile-playpage/track/v3/baseInfo/"+timestamp+"?device=www2&trackQualityLevel=2&trackId=25202",jar,true,false,true});
                var info=response["trackInfo"] as JObject;
                Check(info!=null && (bool)info["isAuthorized"],"official server authorizes the test episode");
                Console.WriteLine("QUALITIES "+string.Join(",",((JArray)info["playUrlList"]).OfType<JObject>().Select(v=>v["type"].ToString()).ToArray()));
                var item=((JArray)info["playUrlList"]).OfType<JObject>().First(v=>v["url"]!=null);
                string address=item["url"].ToString();
                Check(Rules.IsMusicUrl(address) || Rules.IsMusicUrl(Bridge.DecodeUrl(address)),"real authorized URL envelope decodes to official CDN");
                return 0;
            }
            Bridge.Init(dir);Check(!Bridge.IsLoggedIn,"isolated anonymous test");
            byte[] png=Convert.FromBase64String(Bridge.QrImage("ximalaya"));Check(png.Length>100 && png[0]==137,"official QR PNG");
            Check(JsonConvert.DeserializeObject<QrState>(Bridge.CheckQr()).code==66,"QR wait state");
            Bridge.CancelQr();Check(JsonConvert.DeserializeObject<QrState>(Bridge.CheckQr()).code==65,"QR cancellation");
            var songs=Bridge.Playlist("https://www.ximalaya.com/album/2677356");
            Console.WriteLine("ALBUM count="+songs.Count+" first="+(songs.Count>0?songs[0].id:"none"));
            Check(songs.Count==399 && songs.Select(x=>x.id).Distinct().Count()==songs.Count,"all accessible episodes from nine album pages");
            Check(songs[0].id=="25202" && songs[0].duration>1300,"album episode order and duration");
            var single=Bridge.Playlist("track:25202");Check(single.Count==1 && single[0].name.Contains("白眉"),"single episode metadata and UTF8");
            SongUrl audio=JsonConvert.DeserializeObject<SongUrl>(Bridge.SongURL("25202"));Check(Rules.IsMusicUrl(audio.url),"official audio URL");
            var req=(HttpWebRequest)WebRequest.Create(audio.url);req.Timeout=30000;req.ReadWriteTimeout=30000;req.UserAgent=Bridge.UserAgent;req.Referer="https://www.ximalaya.com/";
            byte[] bytes;
            using(var response=req.GetResponse())using(var stream=response.GetResponseStream())using(var buffer=new MemoryStream())
            {byte[] block=new byte[65536];int n;while((n=stream.Read(block,0,block.Length))>0){buffer.Write(block,0,n);if(buffer.Length>64L*1024*1024)throw new Exception("encoded limit");}bytes=buffer.ToArray();}
            using(var pcm=AudioDecoder.Decode(bytes,audio.type,delegate{return false;}))
            {
                double seconds=pcm.Frames/(double)pcm.SampleRate;Check(Math.Abs(seconds-single[0].duration)<5,"complete episode decode");
                var sample=new float[8192];if(pcm.IsStreaming){pcm.Seek(pcm.Frames/2);pcm.Read(sample);}else Array.Copy(pcm.Samples,pcm.Samples.Length/2,sample,0,sample.Length);
                Check(sample.Any(x=>Math.Abs(x)>0.0001f),"non-silent PCM");Console.WriteLine("DECODE seconds="+seconds.ToString("F1")+" streaming="+pcm.IsStreaming+" bytes="+bytes.Length);
            }
            return 0;
        }catch(Exception error){Console.WriteLine("FAIL "+error.GetType().Name+": "+error.Message);return 1;}
    }
}
