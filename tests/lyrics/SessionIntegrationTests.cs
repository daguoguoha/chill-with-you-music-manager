// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
namespace BepInEx { static class Paths { internal static string ConfigPath; } }
namespace BepInEx.Configuration { class ConfigEntry<T> { internal T Value; internal ConfigEntry(T v) { Value = v; } } }
namespace HarmonyLib
{
    class HarmonyMethod { internal HarmonyMethod(Type type, string name) { } }
    static class AccessTools { internal static object PropertySetter(Type t, string s) { return null; } internal static object Method(Type t, string s) { return null; } }
    class Harmony { internal void Patch(object target, HarmonyMethod prefix = null, HarmonyMethod postfix = null) { } }
}
namespace UnityEngine
{
    static class Time { internal static float realtimeSinceStartup; }
    struct Vector2 { internal static Vector2 zero { get { return new Vector2(); } } }
    class AudioClip { internal float length = 120; }
    class AudioSource { internal AudioClip clip; internal float time; internal bool isPlaying, mute; }
    static class Resources { internal static T[] FindObjectsOfTypeAll<T>() { return new[] { (T)(object)KanKikuchi.AudioManager.MusicManager.Current }; } }
}
namespace KanKikuchi.AudioManager
{
    class AudioPlayer { internal UnityEngine.AudioSource AudioSource = new UnityEngine.AudioSource(); }
    class MusicManager { internal static MusicManager Current = new MusicManager(); internal AudioPlayer Player = new AudioPlayer(); internal AudioPlayer GetPlayer(UnityEngine.AudioClip c) { return Player; } }
}
namespace Bulbul
{
    static class SaveDataManager { internal static bool HasInstance = true; }
    class GameAudioInfo { internal string UUID; internal UnityEngine.AudioClip AudioClip = new UnityEngine.AudioClip(); }
    class MusicService { internal GameAudioInfo PlayingMusic = new GameAudioInfo { UUID = "game:original" }; internal bool IsPause; internal List<GameAudioInfo> AllMusicList = new List<GameAudioInfo> { new GameAudioInfo { UUID = "game:original" } }; internal List<GameAudioInfo> CurrentPlayList = new List<GameAudioInfo>(); internal void Pause() { IsPause = true; KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.isPlaying = false; } }
}
namespace ChillNeteasePlaylist
{
    class Song { public long id; public string name; public double duration = 120; }
    class TestConfig { internal BepInEx.Configuration.ConfigEntry<T> Bind<T>(string a, string b, T v, string d) { return new BepInEx.Configuration.ConfigEntry<T>(v); } }
    class TestLog { internal void LogInfo(string s) { } internal void LogWarning(string s) { } }
    public sealed partial class Plugin
    {
        static Plugin Instance;
        const string LyricProvider = "netease", CloudPrefix = "netease:";
        TestConfig Config = new TestConfig(); TestLog Logger = new TestLog(); HarmonyLib.Harmony harmony = new HarmonyLib.Harmony();
        Bulbul.MusicService NativeService = new Bulbul.MusicService();
        Dictionary<string, Song> nativeSongs = new Dictionary<string, Song>(); List<Song> songs = new List<Song>();
        Song[] queue;
        KanKikuchi.AudioManager.MusicManager lyricManager;
        bool ready = true, logged = true, busy;
        long selectedPlaylistId; string selectedPlaylistTitle, query, status; UnityEngine.Vector2 songScroll;
        static bool IsCloud(Bulbul.GameAudioInfo i) { return i != null && i.UUID.StartsWith(CloudPrefix); }
        void RegisterNativePlaylist()
        {
            NativeService.CurrentPlayList.Clear();
            foreach (var s in songs) { string id = CloudPrefix + s.id; nativeSongs[id] = s; NativeService.CurrentPlayList.Add(new Bulbul.GameAudioInfo { UUID = id }); }
        }
        void NativePlayFromPanel(Song[] list, int index)
        {
            SessionManualSelection();
            queue = list;
            NativeService.PlayingMusic = NativeService.CurrentPlayList[index];
            var source = KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource;
            source.clip = NativeService.PlayingMusic.AudioClip; source.time = 0; source.isPlaying = true;
        }
        static int checks;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        void Frame(float seconds) { UnityEngine.Time.realtimeSinceStartup += seconds; PlaybackSessionTickSafe(); }
        static Plugin Boot(string provider, bool paused)
        {
            BepInEx.Paths.ConfigPath = Path.Combine(Path.GetTempPath(), "chill-session-integration-" + Guid.NewGuid().ToString("N"));
            var original = new PlaybackSession { Provider = provider, CurrentId = "2", PlaylistId = "11", PlaylistTitle = "Synthetic playlist", QueueJson = JsonConvert.SerializeObject(new[] { new Song { id = 1, name = "Synthetic 1" }, new Song { id = 2, name = "Synthetic 2" } }), Seconds = 34, Duration = 120, Paused = paused, UpdatedUtcTicks = DateTime.UtcNow.Ticks };
            PlaybackSessionStore.Write(Path.Combine(BepInEx.Paths.ConfigPath, "ChillPlaybackSession", "last.json"), original);
            var plugin = new Plugin(); Instance = plugin; plugin.SetupPlaybackSession(); plugin.sessionReading.Wait(3000); return plugin;
        }
        internal static void Run()
        {
            UnityEngine.Time.realtimeSinceStartup = 0;
            var p = Boot("netease", false); p.Frame(.2f);
            Check(p.sessionApplying && p.NativeService.PlayingMusic.UUID == "netease:2", "startup reconstructs last track rather than first row");
            Check(p.songs.Count == 2 && p.selectedPlaylistId == 11 && p.selectedPlaylistTitle == "Synthetic playlist", "playlist and queue reconstructed");
            p.Frame(.2f); p.Frame(.6f); p.Frame(.2f); p.Frame(.2f);
            Check(!p.sessionApplying && KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource.time == 34, "startup restores exact saved second");
            Check(!p.NativeService.IsPause, "playing session continues playing");
            p.sessionWriting = null;
            var source = KanKikuchi.AudioManager.MusicManager.Current.Player.AudioSource;
            source.time = 53; p.ClosePlaybackSession();
            Check(PlaybackSessionStore.Read(p.sessionPath).Seconds == 53, "normal quit flushes latest position");
            p = Boot("netease", true); p.Frame(.2f); p.Frame(.2f); p.Frame(.6f); p.Frame(.2f); p.Frame(.2f);
            Check(p.NativeService.IsPause && !source.isPlaying && source.time == 34, "paused session restores paused at saved position");
            p.NativeService.IsPause = false; p.SavePlaybackSession(true);
            Check(PlaybackSessionStore.Read(p.sessionPath).Paused, "actual paused audio wins over stale service flag");
            p.NativeService.CurrentPlayList.Clear(); p.SavePlaybackSession(true);
            Check(JsonConvert.DeserializeObject<List<Song>>(PlaybackSessionStore.Read(p.sessionPath).QueueJson).Count == 2, "transient empty native playlist preserves full saved queue");
            p = Boot("netease", false); p.Frame(.2f); p.Frame(.6f);
            Check(p.sessionApplying, "seek is verified on a later audio frame");
            source.time = 0; p.Frame(.2f);
            Check(p.sessionApplying && source.time == 34, "audio-frame seek reset is retried");
            p.Frame(.2f); Check(!p.sessionApplying, "seek completes only after source confirms position");
            p = Boot("qq", false); p.Frame(.2f);
            Check(p.sessionStartupDone && !IsCloud(p.NativeService.PlayingMusic), "only last provider auto-resumes");
            p = Boot("netease", false); p.NativeService.PlayingMusic = new Bulbul.GameAudioInfo { UUID = "bilibili:chosen" }; p.Frame(.2f);
            Check(p.sessionStartupDone && p.NativeService.PlayingMusic.UUID == "bilibili:chosen", "user selection of another provider wins over auto-resume");
            p = Boot("netease", false); p.SessionManualSelection(); p.Frame(.2f);
            Check(!IsCloud(p.NativeService.PlayingMusic), "manual panel selection cancels pending startup restore");
            p = Boot("netease", false); p.logged = false; p.Frame(.2f);
            Check(!p.sessionStartupDone && !IsCloud(p.NativeService.PlayingMusic), "startup waits for existing account login");
            p.Frame(180); Check(!p.sessionStartupDone, "slow account loading does not expire saved session");
            p.logged = true; p.Frame(.2f); Check(p.sessionApplying, "startup retries when bridge becomes ready");
            p = Boot("netease", false); p.ResetPlaybackSessionAccount(); p.Frame(.2f);
            Check(!IsCloud(p.NativeService.PlayingMusic), "account switch cancels old startup restore");
            p = Boot("netease", false); p.NativeService.AllMusicList.Clear(); p.Frame(.2f);
            Check(!p.sessionStartupDone && !IsCloud(p.NativeService.PlayingMusic), "wait for native game music catalog to initialize");
            p.NativeService.AllMusicList.Add(new Bulbul.GameAudioInfo { UUID = "game:original" }); p.Frame(.2f);
            Check(p.sessionApplying, "restore begins after native music catalog is ready");
            p = Boot("netease", false); p.Frame(.2f); p.Frame(.2f); p.Frame(.6f); p.Frame(.2f); p.Frame(.2f);
            p.NativeService.PlayingMusic = new Bulbul.GameAudioInfo { UUID = "game:original" }; SessionAfterTrack();
            Check(PlaybackSessionStore.Read(p.sessionPath).Provider == "game", "selecting original game music suppresses stale cloud resume");
            Console.WriteLine("TOTAL " + checks);
        }
    }
}
class SessionIntegrationMain { static void Main() { ChillNeteasePlaylist.Plugin.Run(); } }
