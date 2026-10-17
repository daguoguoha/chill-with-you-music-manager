// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using Bulbul;
using ChillAudioProgress;
using KanKikuchi.AudioManager;
using Newtonsoft.Json;
using UnityEngine;
class ProgressTests
{
    static string root;
    static int passed;
    static List<string> results=new List<string>();
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;results.Add(name);Console.WriteLine("PASS "+name);}
    static ProgressStore Store(string name){return new ProgressStore(Path.Combine(root,name,"progress.json"),delegate{});}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);}
    static void Call(string name,MusicService service){typeof(Plugin).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{service});}
    static void Tick(Plugin p){typeof(Plugin).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);}
    static Plugin Player(ProgressStore store,MusicService service,MusicManager manager,bool resume=true,bool enabled=true)
    {
        Plugin p=new Plugin();Plugin.Instance=p;
        Set(p,"store",store);Set(p,"savingEnabled",new ConfigEntry<bool>(enabled));Set(p,"resume",new ConfigEntry<bool>(resume));Set(p,"service",service);
        Resources.Items=manager;Time.realtimeSinceStartup=0;return p;
    }
    static GameAudioInfo Track(string id,float duration){return new GameAudioInfo{UUID=id,AudioClip=new AudioClip{length=duration}};}
    static AudioSource Source(MusicManager manager,GameAudioInfo info,float time)
    {var s=new AudioSource{clip=info.AudioClip,time=time,isPlaying=true};manager.Player=new AudioPlayer{AudioSource=s};return s;}
    static int Main(string[] args)
    {
        try
        {
            root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            var store=Store("roundtrip");
            foreach(string prefix in new[]{"netease:","qqmusic:","kugou:","bilibili:","ximalaya:","saltplayer:"})store.Record(prefix+"中文123",123.5,3600);
            store.Flush();store=Store("roundtrip");
            Check(new[]{"netease:","qqmusic:","kugou:","bilibili:","ximalaya:","saltplayer:"}.All(p=>store.Resume(p+"中文123",3600)==123.5),"six platforms and Unicode survive process reload");
            Check(store.Resume("ximalaya:other",3600)==0,"different episodes do not share progress");
            Check(store.Resume("qqmusic:中文123",300)==0,"changed content duration rejects stale bookmark");
            store.Record("ximalaya:long",3.5,36000);Check(store.Resume("ximalaya:long",36000)==3.5,"hours-long episode can restore early seconds");
            store.Record("ximalaya:long",35996,36000);Check(store.Resume("ximalaya:long",36000)==0,"near end restarts at beginning");
            store.Record("netease:中文123",1,3600);Check(store.Resume("netease:中文123",3600)==0,"seek to beginning clears bookmark");
            store.Record("ximalaya:invalid",double.NaN,3600);store.Record("ximalaya:invalid",20,double.PositiveInfinity);store.Record("ximalaya:invalid",-3,3600);store.Record("ximalaya:invalid",4000,3600);
            Check(store.Resume("ximalaya:invalid",3600)==0,"nonfinite negative and out of range times are ignored");
            Check(!ProgressStore.Supported("vanilla:123")&&!ProgressStore.Supported("ximalaya:")&&!ProgressStore.Supported(null),"vanilla and missing identifiers are ignored");
            var backup=Store("backup");backup.Record("bilibili:one",10,100);backup.Flush();backup.Record("bilibili:one",20,100);backup.Flush();
            string bp=Path.Combine(root,"backup","progress.json");File.WriteAllText(bp,"broken");backup=Store("backup");
            Check(backup.Resume("bilibili:one",100)==10&&Directory.GetFiles(Path.GetDirectoryName(bp),"*.corrupt-*").Length==1,"corruption preserves original and restores last good backup");
            backup.Flush();Check(Store("backup").Resume("bilibili:one",100)==10,"recovered document can be persisted");
            var empty=Store("corrupt-only");empty.Record("kugou:a",10,100);empty.Flush();string ep=Path.Combine(root,"corrupt-only","progress.json");File.WriteAllText(ep,"broken");empty=Store("corrupt-only");empty.Record("kugou:b",15,100);empty.Flush();
            Check(Store("corrupt-only").Resume("kugou:b",100)==15&&Directory.GetFiles(Path.GetDirectoryName(ep),"*.corrupt-*").Length==1,"damaged file without backup is preserved before new records");
            string fp=Path.Combine(root,"future","progress.json");Directory.CreateDirectory(Path.GetDirectoryName(fp));File.WriteAllText(fp,"{\"Schema\":9,\"Tracks\":{}}");var future=Store("future");future.Record("qqmusic:a",20,100);future.Flush();
            Check(File.ReadAllText(fp)=="{\"Schema\":9,\"Tracks\":{}}","unknown schema is never overwritten");
            var locked=Store("locked");locked.Record("ximalaya:a",10,100);locked.Flush();string lp=Path.Combine(root,"locked","progress.json");locked.Record("ximalaya:a",30,100);
            using(var file=File.Open(lp,FileMode.Open,FileAccess.Read,FileShare.Read)){locked.Flush();Check(Store("locked").Resume("ximalaya:a",100)==10,"failed atomic replacement retains previous document");}
            locked.Flush();Check(Store("locked").Resume("ximalaya:a",100)==30,"write is retried after lock is released");
            Check(Directory.GetFiles(root,"*.tmp-*",SearchOption.AllDirectories).Length==0,"failed and successful writes leave no partial temp files");
            var bounded=Store("bounded");for(int i=0;i<2010;i++)bounded.Record("saltplayer:"+i,10,100);bounded.Flush();
            Check(JsonConvert.DeserializeObject<ProgressDocument>(File.ReadAllText(Path.Combine(root,"bounded","progress.json"))).Tracks.Count==2000,"history is bounded to 2000 tracks");

            var runtime=Store("runtime");var service=new MusicService();var manager=new MusicManager();
            runtime.Record("ximalaya:book",45,7200);runtime.Flush();runtime=Store("runtime");
            var plugin=Player(runtime,service,manager);var track=Track("ximalaya:book",7200);service.PlayingMusic=track;var source=Source(manager,track,0);Call("AfterPlayingChanged",service);
            Check(source.time==45,"native playback restores saved seconds after restart");
            source.time=65;source.isPlaying=false;Call("SaveNow",service);
            Check(Store("runtime").Resume(track.UUID,7200)==65,"pause saves current position immediately");
            source.time=90;Call("BeforeChange",service);var other=Track("bilibili:part2",3600);service.PlayingMusic=other;Source(manager,other,0);Call("AfterPlayingChanged",service);
            Check(Store("runtime").Resume(track.UUID,7200)==90&&runtime.Resume(other.UUID,3600)==0,"switching audio saves outgoing track without leaking to next");
            source=manager.Player.AudioSource;source.time=130;Call("AfterSeek",service);
            Check(Store("runtime").Resume(other.UUID,3600)==130,"manual seek saves actual source time");
            source.time=0;source.isPlaying=true;Call("AfterSeek",service);
            Check(Store("runtime").Resume(other.UUID,3600)==0,"manual restart at zero clears prior position");
            source.time=180;source.isPlaying=true;Call("SaveNow",service);source.time=0;source.isPlaying=false;Call("SaveNow",service);
            Check(Store("runtime").Resume(other.UUID,3600)==180,"stopped source zero does not erase last position");
            Call("OnCompleted",service);Call("BeforeChange",service);source.isPlaying=true;Call("AfterPlayingChanged",service);
            Check(Store("runtime").Resume(other.UUID,3600)==0&&source.time==0,"natural completion clears position before single-track repeat");
            runtime.Record("saltplayer:later",100,1000);var later=Track("saltplayer:later",1000);service.PlayingMusic=later;manager.Player=null;Call("AfterPlayingChanged",service);source=Source(manager,later,0);Tick(plugin);
            Check(source.time==100,"restoration waits for matching audio source to become ready");
            runtime.Record("qqmusic:pending",75,1000);service.PlayingMusic=Track("qqmusic:pending",1000);manager.Player=null;Call("AfterPlayingChanged",service);service.PlayingMusic=other;source=Source(manager,other,0);Call("AfterPlayingChanged",service);Tick(plugin);
            Check(source.time==0,"canceled loading never seeks a different track");
            source.time=70;Time.realtimeSinceStartup=10;Tick(plugin);
            Check(Store("runtime").Resume(other.UUID,3600)==70,"periodic tick writes progress at five-second intervals");
            runtime.Record(track.UUID,200,7200);plugin=Player(runtime,service,manager,false);service.PlayingMusic=track;source=Source(manager,track,0);Call("AfterPlayingChanged",service);
            Check(source.time==0,"resume can be disabled independently of saving");
            plugin=Player(runtime,service,manager,true,false);Call("AfterPlayingChanged",service);source.time=999;Call("SaveNow",service);
            Check(runtime.Resume(track.UUID,7200)==200,"disabled plugin leaves records and playback untouched");
            Console.WriteLine("TOTAL "+passed+" passed");
            File.WriteAllText(Path.Combine(root,"results.json"),JsonConvert.SerializeObject(new{Passed=passed,Tests=results},Formatting.Indented));
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
