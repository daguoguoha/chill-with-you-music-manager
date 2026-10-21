// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ObservableCollections;
using Bulbul;
using PLATFORM_NAMESPACE;
namespace ObservableCollections { public class ObservableList<T>:List<T> {} }
namespace Bulbul {
    [Flags] public enum AudioTag { All=0, Cloud=256 }
    public class GameAudioInfo { public string UUID; }
    public class TagValue { public AudioTag Value=AudioTag.Cloud; }
    public class Settings { public TagValue CurrentAudioTag=new TagValue(); }
    public class SaveDataManager { public static bool HasInstance=true;public static SaveDataManager Instance=new SaveDataManager();public Settings MusicSetting=new Settings(); }
}
public class MusicService
{
    public List<GameAudioInfo> _allMusicList=new List<GameAudioInfo>(),shuffleList=new List<GameAudioInfo>();
    public ObservableList<GameAudioInfo> CurrentPlayList { get;set; }
    public bool IsShuffle;
    public GameAudioInfo PlayingMusic;
}
namespace PLATFORM_NAMESPACE
{
    public sealed partial class Plugin
    {
        const AudioTag CloudTag=AudioTag.Cloud;
        public static Plugin Instance;
        public MusicService NativeService;
        static bool IsCloud(GameAudioInfo info){return info!=null && info.UUID.StartsWith("own:");}
    }
}
public static class ShufflePlaybackTests
{
    static int checks;
    static void Check(bool value){if(!value)throw new Exception("check "+checks);checks++;}
    static object Call(string name,params object[] args){return typeof(Plugin).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}
    static void Run()
    {
        var a=new GameAudioInfo{UUID="own:a"};var b=new GameAudioInfo{UUID="own:b"};var c=new GameAudioInfo{UUID="own:c"};var d=new GameAudioInfo{UUID="other:d"};
        var service=new MusicService{CurrentPlayList=new ObservableList<GameAudioInfo>(),IsShuffle=true,PlayingMusic=a};
        service._allMusicList.AddRange(new[]{a,b,c,d});service.shuffleList.AddRange(new[]{a,c,d,b});service.CurrentPlayList.AddRange(new[]{a,c,b});
        Plugin.Instance=new Plugin{NativeService=service};
        Check(Object.ReferenceEquals(service._allMusicList,Call("NativeDisplaySource",service.shuffleList)));
        service.CurrentPlayList.Clear();service.CurrentPlayList.AddRange(new[]{a,b,c});
        Check(service.CurrentPlayList.SequenceEqual(new[]{a,b,c}));
        var queue=(ObservableList<GameAudioInfo>)Call("NativeShuffleRead",service.CurrentPlayList);
        Check(queue.SequenceEqual(new[]{a,c,b}));
        Check(queue[queue.IndexOf(a)+1]==c && service.CurrentPlayList[1]==b);
        Check(Object.ReferenceEquals(queue,Call("NativeShuffleRead",service.CurrentPlayList)));
        Check(queue[(queue.IndexOf(c)+queue.Count-1)%queue.Count]==a);
        Check((int)Call("NativeFailureOffset",service,c)==2);
        service.IsShuffle=false;Check(Object.ReferenceEquals(service.CurrentPlayList,Call("NativeShuffleRead",service.CurrentPlayList)));
        service.IsShuffle=true;service.CurrentPlayList.Add(d);Check(Object.ReferenceEquals(service.CurrentPlayList,Call("NativeShuffleRead",service.CurrentPlayList)));
        service.CurrentPlayList.Remove(d);service.CurrentPlayList.Remove(b);
        Check(((ObservableList<GameAudioInfo>)Call("NativeShuffleRead",service.CurrentPlayList)).SequenceEqual(new[]{a,c}));
        service.shuffleList=new List<GameAudioInfo>{c,a};
        Check(((ObservableList<GameAudioInfo>)Call("NativeShuffleRead",service.CurrentPlayList)).SequenceEqual(new[]{c,a}));
        service.CurrentPlayList.Clear();Check(Object.ReferenceEquals(service.CurrentPlayList,Call("NativeShuffleRead",service.CurrentPlayList)));
        var getter=AccessTools.PropertyGetter(typeof(MusicService),"CurrentPlayList");
        IEnumerable<CodeInstruction> il=new[]{new CodeInstruction(OpCodes.Call,getter),new CodeInstruction(OpCodes.Ret)};
        for(int n=0;n<5;n++)il=((IEnumerable<CodeInstruction>)Call("NativeShufflePlayback",il)).ToList();
        var compiled=il.ToList();Check(compiled.Count(x=>Equals(x.operand,getter))==1);
        Check(compiled.Count(x=>x.operand is MethodInfo && ((MethodInfo)x.operand).Name=="NativeShuffleRead")==5);
        IEnumerable<CodeInstruction> displayIl=new[]{new CodeInstruction(OpCodes.Ldfld,AccessTools.Field(typeof(MusicService),"shuffleList")),new CodeInstruction(OpCodes.Stloc_0)};
        for(int n=0;n<5;n++)displayIl=((IEnumerable<CodeInstruction>)Call("NativeShuffleDisplaySource",displayIl)).ToList();
        Check(displayIl.Count(x=>x.operand is MethodInfo && ((MethodInfo)x.operand).Name=="NativeDisplaySource")==5);
        SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value=AudioTag.All;
        Check(Object.ReferenceEquals(service.shuffleList,Call("NativeDisplaySource",service.shuffleList)));
        Plugin.Instance=null;Check(Object.ReferenceEquals(service.CurrentPlayList,Call("NativeShuffleRead",service.CurrentPlayList)));
        Console.WriteLine("PASS "+checks+" display/playback integration checks");
    }
    public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.GetType().Name+" "+e.Message);return 1;}}
}
