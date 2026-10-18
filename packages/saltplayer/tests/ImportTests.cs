using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ChillSaltPlayerPlaylist;
class ImportTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static string Digest(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(file));}
    static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]),music=Path.Combine(root,"music"),store=Path.Combine(root,"library-"+Guid.NewGuid().ToString("N"));
            Bridge.Init(store);
            foreach(string input in new[]{"https://example.invalid/a.mp3",@"\\server\share\a.mp3",@"\\.\C:\a.mp3","C:relative.mp3","C:\\file.mp3:secret","file://server/share/a.wav"})Check(Rules.LocalPath(input,null)==null,"nonlocal or device path rejected");
            Check(!Rules.IsAudio("bad\0.wav"),"invalid path characters rejected");
            var mixed=Bridge.Import(Path.Combine(root,"mixed.txt"),music);
            Check(mixed.playlist.songs.Count==2 && mixed.missing==1 && mixed.ambiguous==1 && mixed.unsupported==1,"Android remap reports missing, ambiguous and remote rows");
            Check(mixed.playlist.songs[1].path.EndsWith("A\\same.wav"),"directory suffix resolves duplicate filenames correctly");
            var m3u=Bridge.Import(Path.Combine(root,"m3u8.m3u8"),"");
            Check(m3u.playlist.songs.Count==3 && m3u.playlist.songs.Select(s=>s.id).Distinct().Count()==3,"playlist order and intentional repeats preserved");
            Check(m3u.playlist.songs[0].name=="第一首" && m3u.playlist.songs[1].name=="第二首" && m3u.playlist.songs[0].duration==120,"M3U title, artist and duration");
            Check(Bridge.Import(Path.Combine(root,"utf16.txt"),"").playlist.songs.Count==1,"UTF16 playlist");
            Check(Bridge.Import(Path.Combine(root,"gb18030.txt"),"").playlist.songs.Count==1,"GB18030 playlist");
            Check(Bridge.Import(music,"").playlist.songs.Count==4,"recursive folder import");
            Check(Bridge.Import(Path.Combine(music,"原创测试 - 旋律01.wav"),"").playlist.songs.Count==1,"single file import");
            int before=Bridge.Lists().Count;Bridge.Import(Path.Combine(root,"m3u8.m3u8"),"");Check(Bridge.Lists().Count==before,"reimport updates existing playlist");
            string saved=Digest(Path.Combine(store,"library.json"));bool rejected=false;
            try{Bridge.Import(Path.Combine(root,"only-missing.txt"),"");}catch(InvalidOperationException){rejected=true;}
            Check(rejected && Digest(Path.Combine(store,"library.json"))==saved,"failed import preserves saved library");
            rejected=false;try{Bridge.Import(Path.Combine(root,"net.m3u8"),"");}catch(InvalidOperationException){rejected=true;}Check(rejected,"network HLS playlist rejected");
            Bridge.Init(store);Check(Bridge.Lists().Count==before && Bridge.Playlist(m3u.playlist.id).Count==3,"playlist persistence reload");
            string large=Path.Combine(root,"long-test.wav"),hash=Digest(large),pcmPath;
            Check(new FileInfo(large).Length>128L*1024*1024,"test input exceeds old cloud download limit");
            using(var pcm=AudioDecoder.DecodeFile(large,delegate{return false;}))
            {
                Check(pcm.IsStreaming && pcm.Samples==null,"large local audio streams decoded PCM");
                Check(Math.Abs(pcm.Frames/(double)pcm.SampleRate-780)<0.1,"complete large file duration");
                pcmPath=(string)typeof(DecodedAudio).GetField("pcmPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pcm);
                float[] a=new float[8192],b=new float[8192];pcm.Seek(pcm.Frames/2);pcm.Read(a);pcm.Seek(pcm.Frames/2);pcm.Read(b);
                Check(a.SequenceEqual(b) && a.Any(v=>Math.Abs(v)>0.0001f),"repeatable seek with non-silent PCM");
                pcm.Seek(pcm.Frames);pcm.Read(b);Check(b.All(v=>v==0),"EOF safely pads silence");
            }
            Check(!File.Exists(pcmPath) && Digest(large)==hash,"PCM disposal leaves original file unchanged");
            var old=Directory.GetFiles(Path.GetTempPath(),"ChillSaltPlayer-*.pcm").OrderBy(x=>x).ToArray();int calls=0;bool canceled=false;
            try{using(var pcm=AudioDecoder.DecodeFile(large,delegate{return ++calls>3;})){}}catch(OperationCanceledException){canceled=true;}
            Check(canceled && old.SequenceEqual(Directory.GetFiles(Path.GetTempPath(),"ChillSaltPlayer-*.pcm").OrderBy(x=>x)),"canceled load cleans temporary PCM");
            Console.WriteLine("ALL IMPORT AND LOCAL DECODER TESTS PASSED");return 0;
        }
        catch(Exception error){Console.WriteLine("FAIL "+error.GetType().Name+": "+error.Message);return 1;}
    }
}
