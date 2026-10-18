using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using ChillBilibiliPlaylist;
class BilibiliLongTests
{
    static void Check(bool pass,string message) { if(!pass) throw new Exception(message); Console.WriteLine("PASS "+message); }
    static int Main(string[] args)
    {
        try {
            if(args.Length==0) {Console.WriteLine("Usage: BilibiliLongTests.exe <long-video-BV> [...]");return 2;}
            Bridge.Init(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"long-test-account"));
            foreach(string bvid in args) {
                var songs=Bridge.Playlist(bvid); Check(songs.Count>0,"long video metadata "+bvid);
                var audio=JsonConvert.DeserializeObject<SongUrl>(Bridge.SongURL(songs[0].id));
                var req=(HttpWebRequest)WebRequest.Create(audio.url); req.UserAgent=Bridge.UserAgent; req.Referer="https://www.bilibili.com/"; req.Timeout=30000; req.ReadWriteTimeout=30000;
                byte[] bytes;
                using(var response=req.GetResponse()) using(var input=response.GetResponseStream()) using(var buffer=new MemoryStream()) {
                    byte[] block=new byte[65536]; int count;
                    while((count=input.Read(block,0,block.Length))>0) {buffer.Write(block,0,count);if(buffer.Length>64L*1024*1024)throw new Exception("Encoded limit exceeded");}
                    bytes=buffer.ToArray();
                }
                Console.WriteLine("Downloaded "+bvid+" bytes="+bytes.Length);
                string path;
                using(var pcm=AudioDecoder.Decode(bytes,"m4a",delegate{return false;})) {
                    Check(pcm.IsStreaming && pcm.Samples==null,"long video uses local PCM stream, not a giant float array");
                    double seconds=pcm.Frames/(double)pcm.SampleRate;
                    Check(Math.Abs(seconds-songs[0].duration)<5,"complete duration "+seconds.ToString("F1")+" seconds");
                    path=(string)typeof(DecodedAudio).GetField("pcmPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pcm);
                    Check(File.Exists(path),"PCM cache exists during playback");
                    var first=new float[8192]; var again=new float[8192];
                    pcm.Seek(30*pcm.SampleRate);pcm.Read(first);
                    pcm.Seek(30*pcm.SampleRate);pcm.Read(again);
                    Check(first.SequenceEqual(again) && first.Any(x=>Math.Abs(x)>0.0001f),"seeking reproduces non-silent samples");
                    pcm.Seek(pcm.Frames);pcm.Read(again);Check(again.All(x=>x==0),"EOF callback pads silence safely");
                    pcm.Seek(pcm.Frames/2);pcm.Read(again);Check(again.Any(x=>Math.Abs(x)>0.0001f),"seek to middle works");
                }
                Check(!File.Exists(path),"disposing playback deletes PCM cache");
                string[] before=Directory.GetFiles(Path.GetTempPath(),"ChillBilibili-*.pcm");
                int calls=0;bool canceled=false;
                try{using(var unused=AudioDecoder.Decode(bytes,"m4a",delegate{return ++calls>3;})) {}}
                catch(OperationCanceledException){canceled=true;}
                Check(canceled && Directory.GetFiles(Path.GetTempPath(),"ChillBilibili-*.pcm").OrderBy(x=>x).SequenceEqual(before.OrderBy(x=>x)),"canceled decode deletes its temporary cache");
            }
            return 0;
        }catch(Exception error){Console.WriteLine("FAIL "+error.GetType().Name+": "+error.Message);return 1;}
    }
}
