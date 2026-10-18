using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using ChillBilibiliPlaylist;
class BilibiliSmoke
{
    static void Check(bool ok,string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS "+message); }
    static int Main(string[] args)
    {
        try {
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-account");
            if (args.Length>0 && args[0]=="rules") {
                Check(Rules.PlaylistId("BV1GJ411x7h7")=="video:BV1GJ411x7h7:all","raw BV import");
                Check(Rules.PlaylistId("https://www.bilibili.com/video/BV1GJ411x7h7?p=2")=="video:BV1GJ411x7h7:2","specific part import");
                Check(Rules.PlaylistId("https://space.bilibili.com/1/favlist?fid=12345")=="fav:12345","favorite link import");
                Check(Rules.PlaylistId("https://www.bilibili.com/list/ml12345")=="fav:12345","list link import");
                Check(Rules.PlaylistId("https://b23.tv/abc")=="short:https://b23.tv/abc","short link accepted for constrained resolution");
                foreach (string url in new[] {"https://evil.test/video/BV1GJ411x7h7","https://www.bilibili.com.evil.test/video/BV1GJ411x7h7","https://user:pass@www.bilibili.com/video/BV1GJ411x7h7","http://127.0.0.1/video/BV1GJ411x7h7","https://www.bilibili.com/video/BV1GJ411x7h7?p=-1","https://www.bilibili.com:9999/video/BV1GJ411x7h7"})
                    Check(Rules.PlaylistId(url)==null,"unsafe or invalid import rejected");
                foreach (string url in new[] {"https://evil.test/a.m4a","https://x.bilivideo.com.evil.test/a","https://user:pass@x.bilivideo.com/a","https://127.0.0.1/a","https://x.bilivideo.com:123/a"})
                    Check(!Rules.IsMusicUrl(url),"foreign media URL rejected");
                Check(Rules.SecureMusicUrl("http://upos.test.bilivideo.com/a")=="https://upos.test.bilivideo.com/a","media transport upgraded to HTTPS");
                return 0;
            }
            Bridge.Init(dir);
            if(args.Length>0 && args[0]=="short") {
                var imported=Bridge.Playlist("https://b23.tv/BV1GJ411x7h7");
                Check(imported.Count==1 && imported[0].bvid=="BV1GJ411x7h7","real short link resolves to expected video");return 0;
            }
            Check(!Bridge.IsLoggedIn,"test starts with no account");
            byte[] qr=Convert.FromBase64String(Bridge.QrImage("bilibili"));
            Check(qr.Length>100 && qr[0]==137 && qr[1]==80,"real QR generated locally");
            QrState state=JsonConvert.DeserializeObject<QrState>(Bridge.CheckQr());
            Check(state.code==66 || state.code==67,"real QR polling");
            Bridge.CancelQr(); Check(JsonConvert.DeserializeObject<QrState>(Bridge.CheckQr()).code==65,"QR cancellation");
            List<Song> songs=Bridge.Playlist("video:BV1GJ411x7h7:all");
            Check(songs.Count==1 && songs[0].cid>0 && songs[0].duration>200,"real video metadata and parts");
            SongUrl audio=JsonConvert.DeserializeObject<SongUrl>(Bridge.SongURL(songs[0].id));
            Check(audio.type=="m4a" && Rules.IsMusicUrl(audio.url),"real official AAC stream resolved");
            var req=(HttpWebRequest)WebRequest.Create(audio.url); req.UserAgent=Bridge.UserAgent; req.Referer="https://www.bilibili.com/"; req.Timeout=30000;
            byte[] bytes;
            using(var response=req.GetResponse()) using(var stream=response.GetResponseStream()) using(var buffer=new MemoryStream()) {
                byte[] block=new byte[8192];int count;
                while((count=stream.Read(block,0,block.Length))>0) { buffer.Write(block,0,count); if(buffer.Length>64*1024*1024) throw new Exception("Audio too big"); }
                bytes=buffer.ToArray();
            }
            using(var pcm=AudioDecoder.Decode(bytes,"m4a",delegate{return false;})) {
            double duration=pcm.Samples.Length/(double)pcm.Channels/pcm.SampleRate,energy=0;
            foreach(float value in pcm.Samples) energy+=value*value;
            Check(duration>200 && duration<220 && energy>0.01,"real M4A decodes to non-silent full PCM");
            Console.WriteLine("DECODE seconds="+duration.ToString("F1")+" rate="+pcm.SampleRate+" channels="+pcm.Channels+" bytes="+bytes.Length);
            }
            return 0;
        } catch(Exception ex) { Console.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message); return 1; }
    }
}
