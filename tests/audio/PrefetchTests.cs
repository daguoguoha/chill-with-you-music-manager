// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Bulbul;
using UnityEngine;
using Cysharp.Threading.Tasks;
using ChillNeteasePlaylist;
namespace UnityEngine { public class AudioClip { public int samples=44100, channels=2; } public static class Time { public static float realtimeSinceStartup=20; } }
namespace Cysharp.Threading.Tasks {
 public enum UniTaskStatus { Pending, Succeeded, Canceled, Faulted }
 public class FakeTask<T> { public UniTaskStatus Status; public T Value; public FakeTask<T> GetAwaiter(){return this;} public T GetResult(){if(Status==UniTaskStatus.Canceled)throw new OperationCanceledException();if(Status==UniTaskStatus.Faulted)throw new Exception();return Value;} }
 public class UniTaskCompletionSource<T> { public FakeTask<T> Task=new FakeTask<T>(); }
}
namespace Bulbul {
 public class GameAudioInfo { public string UUID; public AudioClip AudioClip; }
 public class Settings { public List<string> ExcludedFromPlaylistUUIDs=new List<string>(); }
 public class SaveDataManager { public static bool HasInstance=true; public static SaveDataManager Instance=new SaveDataManager(); public Settings MusicSetting=new Settings(); }
}
public class MusicService { public List<GameAudioInfo> CurrentPlayList=new List<GameAudioInfo>(); public GameAudioInfo PlayingMusic; public bool IsRepeatOneMusic; }
namespace ChillNeteasePlaylist {
 internal class SongUnavailableException : Exception { }
 internal class ProgressiveAudio { internal bool Complete; internal Exception Failure; }
 public sealed partial class Plugin {
  internal MusicService NativeService=new MusicService(); internal bool destroying; internal float cacheChangedAt; internal int nativeLoadVersion;
  internal GameAudioInfo pendingAudio; internal Dictionary<AudioClip,ProgressiveAudio> progressiveClips=new Dictionary<AudioClip,ProgressiveAudio>();
  internal List<GameAudioInfo> nativeClips=new List<GameAudioInfo>(); internal List<IEnumerator> Jobs=new List<IEnumerator>(); internal List<CancellationToken> Tokens=new List<CancellationToken>(); internal bool Fail;
  internal class Log {public void LogInfo(string s){} public void LogWarning(string s){} } internal Log Logger=new Log();
  static bool IsCloud(GameAudioInfo i){return i!=null&&i.UUID.StartsWith("own:");} static bool OwnPlaylist(MusicService s){return s!=null&&s.CurrentPlayList.Count>0;}
  static IList<GameAudioInfo> NativeShuffleRead(List<GameAudioInfo> q){return q;} static long CloudSongId(GameAudioInfo i){return 0;}
  void StartCoroutine(IEnumerator job){Jobs.Add(job);}
  IEnumerator NativeLoadClip(GameAudioInfo i,CancellationToken token,int v,UniTaskCompletionSource<AudioClip> completion,bool background){Tokens.Add(token);yield return null;if(token.IsCancellationRequested)completion.Task.Status=UniTaskStatus.Canceled;else if(Fail)completion.Task.Status=UniTaskStatus.Faulted;else {i.AudioClip=new AudioClip();nativeClips.Add(i);completion.Task.Value=i.AudioClip;completion.Task.Status=UniTaskStatus.Succeeded;}}
  void ReleaseNativeClip(GameAudioInfo i){i.AudioClip=null;}
  public void Tick(){NativePrefetchTick();} public void Cancel(){CancelNativePrefetch();} public void Trim(){TrimNativeAudioCache(null);}
  public void Promote(int version,CancellationToken token){prefetchPromoted=true;prefetchSelectionVersion=version;nativeLoadVersion=version;prefetchSelectionCancellation=token;}
  public bool SelectionCanceled(GameAudioInfo info){return PrefetchSelectionCanceled(info);}
 }
}
public static class PrefetchTests {
 static int checks; static void Check(bool x){if(!x)throw new Exception("prefetch check "+checks);checks++;}
 public static void Run(){
  var a=new GameAudioInfo{UUID="own:a",AudioClip=new AudioClip()};var b=new GameAudioInfo{UUID="own:b"};var c=new GameAudioInfo{UUID="own:c"};
  var q=new List<GameAudioInfo>{a,b,c};Func<GameAudioInfo,bool> ok=x=>true;
  Check(PrefetchPolicy.Next(q,a,false,ok)==b);Check(PrefetchPolicy.Next(q,c,false,ok)==a);
  Check(PrefetchPolicy.Next(new List<GameAudioInfo>{a,c,b},a,false,ok)==c);
  Check(PrefetchPolicy.Next(q,a,false,x=>x!=b)==c);Check(PrefetchPolicy.Next(q,a,false,x=>false)==null);
  Check(PrefetchPolicy.Next(q,a,true,ok)==null);Check(PrefetchPolicy.Next(new List<GameAudioInfo>{a},a,false,ok)==null);
  Check(PrefetchPolicy.Next(new List<GameAudioInfo>(),a,false,ok)==null);Check(PrefetchPolicy.Next(q,null,false,ok)==null);
  Check(PrefetchPolicy.Next(new List<GameAudioInfo>{a,null,c},a,false,ok)==c);
  Check(ClassicPlaybackPolicy.Compatibility(new PlaybackFailure("x","ConnectionError_HTTP_0","x",true)));
  Check(ClassicPlaybackPolicy.Compatibility(new PlaybackFailure("x","ProtocolError_HTTP_302","x",true)));
  Check(!ClassicPlaybackPolicy.Compatibility(new PlaybackFailure("x","ProtocolError_HTTP_403","x",true)));
  Check(!ClassicPlaybackPolicy.Compatibility(null));Check(ClassicPlaybackPolicy.CanStream("mp3",false,300));Check(!ClassicPlaybackPolicy.CanStream("flac",false,300));Check(!ClassicPlaybackPolicy.CanStream("mp3",true,300));
  Check(ClassicPlaybackPolicy.Compatibility(new PlaybackFailure("x","BUFFER_FALLBACK","x",true)));
  Check(ClassicPlaybackPolicy.KeepRecent(true,false,false,false,9,0,long.MaxValue));
  Check(ClassicPlaybackPolicy.KeepRecent(false,true,false,false,9,0,long.MaxValue));
  Check(ClassicPlaybackPolicy.KeepRecent(false,false,true,true,2,0,1024));
  Check(!ClassicPlaybackPolicy.KeepRecent(false,false,true,true,3,0,1024));
  Check(!ClassicPlaybackPolicy.KeepRecent(false,false,true,false,0,0,1024));
  Check(!ClassicPlaybackPolicy.KeepRecent(false,false,true,true,0,ClassicPlaybackPolicy.RecentBytes,1));
  var p=new Plugin();p.NativeService.CurrentPlayList=q;p.NativeService.PlayingMusic=a;
  p.Tick();Check(p.Jobs.Count==1);p.Jobs[0].MoveNext();Check(!p.Tokens[0].IsCancellationRequested);
  p.Promote(7,CancellationToken.None);Check(!p.SelectionCanceled(b));p.nativeLoadVersion++;Check(p.SelectionCanceled(b));Check(!p.SelectionCanceled(c));
  using(var selection=new CancellationTokenSource()){p.Promote(8,selection.Token);Check(!p.SelectionCanceled(b));selection.Cancel();Check(p.SelectionCanceled(b));}
  p.Tick();Check(p.Jobs.Count==1);Check(p.NativeService.PlayingMusic==a && b.AudioClip==null);
  SaveDataManager.Instance.MusicSetting.ExcludedFromPlaylistUUIDs.Add(b.UUID);p.Tick();Check(p.Tokens[0].IsCancellationRequested);Check(p.Jobs.Count==2);
  p.Jobs[0].MoveNext();p.Jobs[1].MoveNext();p.Jobs[1].MoveNext();Check(c.AudioClip!=null && p.NativeService.PlayingMusic==a);
  p.Tick();Check(p.Jobs.Count==2);
  p.progressiveClips[c.AudioClip]=new ProgressiveAudio();p.Trim();Check(c.AudioClip!=null);
  p.progressiveClips[c.AudioClip].Failure=new Exception();p.Trim();Check(c.AudioClip==null);
  p.Cancel();SaveDataManager.Instance.MusicSetting.ExcludedFromPlaylistUUIDs.Clear();
  c.AudioClip=new AudioClip();
  var p2=new Plugin();p2.NativeService.CurrentPlayList=q;p2.NativeService.PlayingMusic=a;p2.pendingAudio=b;p2.Tick();Check(p2.Jobs.Count==0);
  p2.pendingAudio=null;p2.progressiveClips[a.AudioClip]=new ProgressiveAudio();p2.Tick();Check(p2.Jobs.Count==0);
  p2.progressiveClips[a.AudioClip].Complete=true;p2.Fail=true;p2.Tick();Check(p2.Jobs.Count==1);p2.Jobs[0].MoveNext();p2.Jobs[0].MoveNext();p2.Tick();Check(p2.Jobs.Count==1);
  p2.nativeClips.Add(a);p2.nativeClips.Add(b);p2.nativeClips.Add(c);p2.Trim();Check(a.AudioClip!=null && c.AudioClip!=null);
  p2.Cancel();Console.WriteLine("PASS "+checks+" prefetch, cancellation, queue and cache checks");
 }
}
