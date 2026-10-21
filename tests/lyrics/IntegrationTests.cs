// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace BepInEx { static class Paths { internal static string ConfigPath = System.IO.Path.GetTempPath(); } }
namespace BepInEx.Configuration
{
    class ConfigEntry<T> { public T Value; public ConfigEntry(T v) { Value = v; } }
    class ConfigDescription { public ConfigDescription(string s, object range) { } }
    class AcceptableValueRange<T> { public AcceptableValueRange(T a, T b) { } }
}
namespace UnityEngine
{
    static class Time { internal static float realtimeSinceStartup; }
    class AudioClip { }
    class AudioSource { internal AudioClip clip; internal float time; }
    class GameObject { internal T GetComponent<T>() where T : class { return null; } }
    static class Resources { public static T[] FindObjectsOfTypeAll<T>() { return new[] { (T)(object)KanKikuchi.AudioManager.MusicManager.Current }; } }
}
namespace UnityEngine.EventSystems { class EventSystem { internal static EventSystem current; internal UnityEngine.GameObject currentSelectedGameObject; } }
namespace UnityEngine.UI { class InputField { internal bool isFocused; } }
namespace TMPro { class TMP_InputField { internal bool isFocused; } }
namespace KanKikuchi.AudioManager
{
    class AudioPlayer { internal UnityEngine.AudioSource AudioSource = new UnityEngine.AudioSource(); }
    class MusicManager { internal static MusicManager Current = new MusicManager(); internal AudioPlayer Player = new AudioPlayer(); internal AudioPlayer GetPlayer(UnityEngine.AudioClip c) { return Player; } }
}
namespace Bulbul
{
    class GameAudioInfo { internal string UUID; internal UnityEngine.AudioClip AudioClip = new UnityEngine.AudioClip(); }
    class MusicService { internal GameAudioInfo PlayingMusic; internal bool IsPause; }
}
namespace ChillNeteasePlaylist
{
    class PlaybackFailure : Exception { internal string Code; }
    static class RuntimeHost { internal static bool VDown, Modifier; internal static bool IsVDownInGame() { return VDown; } internal static bool HasLyricShortcutModifier() { return Modifier; } }
    class Song { internal long id; internal string name; internal double duration = 120; }
    class TestConfig
    {
        internal BepInEx.Configuration.ConfigEntry<T> Bind<T>(string a, string b, T value, object description) { return new BepInEx.Configuration.ConfigEntry<T>(value); }
        internal void Save() { }
    }
    class TestLog { internal void LogInfo(string s) { } internal void LogWarning(string s) { } }
    sealed class LyricTrack { internal string Key, Title, Id, Hash, LocalPath; internal double Duration; }
    sealed class LyricResult { internal LyricDocument Document; internal LyricDocument Translation = new LyricDocument(); internal string Message; }
    static class LyricSource
    {
        internal static ManualResetEventSlim Started = new ManualResetEventSlim(false), Release = new ManualResetEventSlim(false);
        internal static bool Delayed;
        internal static int Calls;
        internal static LyricResult Load(string provider, string dir, LyricTrack track, bool refresh, CancellationToken cancel)
        {
            Interlocked.Increment(ref Calls);
            if (Delayed) { Started.Set(); Release.Wait(3000); }
            return new LyricResult { Document = LyricDocument.Parse("[00:01]Synthetic " + track.Id + "\n[00:10]Next " + track.Id), Message = "loaded " + track.Id };
        }
        internal static LyricResult WithTranslation(LyricResult result,string provider,string dir,LyricTrack track,bool refresh,CancellationToken cancel)
        { result.Translation=LyricDocument.Parse("[00:01]Synthetic translated "+track.Id);return result; }
        internal static LyricResult ImportTranslation(LyricResult original,string dir,LyricTrack track,string file,string paste,CancellationToken cancel)
        { original.Translation=LyricDocument.Parse(paste);return original; }
        internal static LyricResult Import(string dir, LyricTrack track, string file, string paste, CancellationToken cancel) { return Load("", dir, track, false, cancel); }
    }
    public sealed partial class Plugin
    {
        TestConfig Config = new TestConfig(); TestLog Logger = new TestLog();
        void FlushLyricDrag() { }
        Bulbul.MusicService NativeService = new Bulbul.MusicService();
        Dictionary<string, Song> nativeSongs = new Dictionary<string, Song>();
        static bool IsCloud(Bulbul.GameAudioInfo i) { return i != null && i.UUID.StartsWith("netease:"); }
        static int assertions;
        static void Check(bool value, string name) { if (!value) throw new Exception(name); assertions++; Console.WriteLine("PASS " + name); }
        void Frame() { UnityEngine.Time.realtimeSinceStartup += .2f; LyricsTickSafe(); }
        void Complete() { if (lyricTask != null) lyricTask.Wait(4000); Frame(); }
        void Select(long id)
        {
            string key = "netease:" + id;
            nativeSongs[key] = new Song { id = id, name = "Synthetic " + id };
            NativeService.PlayingMusic = new Bulbul.GameAudioInfo { UUID = key };
            KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.clip = NativeService.PlayingMusic.AudioClip;
            KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.time = 2;
            Frame();
        }
        internal static void Run()
        {
            var p = new Plugin(); p.SetupLyrics();
            LyricSource.Delayed = true; p.Select(1);
            Check(LyricSource.Started.Wait(1000), "first lyric fetch starts asynchronously");
            p.Select(2);
            Check(p.lyricTrack.Id == "2" && p.lyricDocument.Lines.Count == 0, "track change immediately clears old lyrics");
            Check(LyricSource.Calls == 1, "only one fetch can remain in flight");
            LyricSource.Delayed = false; LyricSource.Release.Set(); p.Complete(); p.Complete();
            Check(p.lyricDocument.Lines.Count == 2 && p.lyricDocument.Lines[0].Text == "Synthetic 2", "late response from old track discarded");
            Check(p.lyricPosition == 2, "reads real audio source position");
            NativePause(p, true); p.Frame(); double position = p.lyricPosition;
            UnityEngine.Time.realtimeSinceStartup += 100; p.Frame();
            Check(p.lyricPosition == position, "paused lyric does not advance on wall clock");
            KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.time = 30; p.Frame();
            Check(p.lyricDocument.IndexAt(p.lyricPosition) == 1, "forward seek selects correct lyric");
            KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.time = 0; p.Frame();
            Check(p.lyricDocument.IndexAt(p.lyricPosition) == -1, "backward seek clears premature lyric");
            p.lyricsOffset.Value = 2; p.Frame();
            Check(p.lyricDocument.IndexAt(p.lyricPosition + p.lyricsOffset.Value) == 0, "user timing correction applied");
            p.lyricsFile = "synthetic.lrc"; p.lyricsPaste = "old track draft"; p.lyricsImportKey = "netease:2"; p.Select(3);
            Check(p.lyricsPaste == "" && p.lyricsFile == "" && p.lyricsImportKey == null, "track change clears import draft identity");
            p.RequestLyrics(true);
            Check(p.lyricsMessage.Contains("曲目已切换"), "stale import rejected");
            p.Complete(); p.ToggleLyrics();
            Check(!p.lyricsEnabled.Value, "lyric hide toggle independent from playback");
            Check(p.NativeService.PlayingMusic.UUID == "netease:3", "hide does not alter track");
            p.ToggleLyrics(); p.Frame();
            Check(p.lyricsEnabled.Value, "lyric show toggle restores display");
            RuntimeHost.VDown = true; p.Frame();
            Check(!p.lyricsEnabled.Value, "V hides current provider lyrics");
            p.Frame(); Check(!p.lyricsEnabled.Value, "held V toggles only once");
            RuntimeHost.VDown = false; p.Frame(); RuntimeHost.VDown = true; p.Frame();
            Check(p.lyricsEnabled.Value, "second V shows lyrics");
            RuntimeHost.VDown = false; p.Frame(); p.lyricTyping = true; RuntimeHost.VDown = true; p.Frame();
            Check(p.lyricsEnabled.Value, "typing V does not toggle overlay");
            p.lyricTyping = false; RuntimeHost.VDown = false; p.Frame(); RuntimeHost.Modifier = true; RuntimeHost.VDown = true; p.Frame();
            Check(p.lyricsEnabled.Value, "Ctrl V and modified chords do not toggle");
            RuntimeHost.Modifier = false; p.Frame();
            Check(p.lyricsEnabled.Value, "modifier release while V held does not toggle");
            RuntimeHost.VDown = false; p.Frame();
            p.NativeService.PlayingMusic = new Bulbul.GameAudioInfo { UUID = "qq:4" }; p.Frame();
            Check(p.lyricTrack == null && p.lyricDocument.Lines.Count == 0, "other provider does not reuse overlay");
            p.Select(5);p.Complete();
            Check(!p.lyricsTranslationEnabled.Value && p.lyricTranslation.Lines.Count==0,"translation is opt-in by default");
            p.ToggleLyricTranslation();p.Frame();p.Complete();
            Check(p.lyricsTranslationEnabled.Value && p.lyricTranslation.Lines.Count==1,"toggle loads provider translation");
            Check(p.lyricDocument.Lines[0].Text=="Synthetic 5","translation preserves original");
            int calls=LyricSource.Calls;p.ToggleLyricTranslation();p.Frame();
            Check(!p.lyricsTranslationEnabled.Value && LyricSource.Calls==calls,"hide translation without network or track change");
            p.ToggleLyricTranslation();p.Frame();Check(LyricSource.Calls==calls,"show cached translation immediately");
            p.Select(6);Check(p.lyricTranslation.Lines.Count==0,"track switch clears previous translation immediately");p.Complete();
            Check(p.lyricTranslation.Lines[0].Text.Contains("6"),"new track receives own translation");
            p.CloseLyrics(); p.Select(5);
            Check(p.lyricTrack != null && p.lyricTrack.Id == "6", "destroyed plugin ignores further playback");
            Console.WriteLine("TOTAL " + assertions);
        }
        static void NativePause(Plugin p, bool value) { p.NativeService.IsPause = value; }
    }
}
class IntegrationTestMain { static void Main() { ChillNeteasePlaylist.Plugin.Run(); } }
