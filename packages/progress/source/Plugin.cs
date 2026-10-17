// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Bulbul;
using HarmonyLib;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace ChillAudioProgress
{
    [BepInPlugin("local.chill.audioprogress", "音频进度保存", "1.0.0")]
    [BepInProcess("Chill With You.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        Harmony harmony;
        ProgressStore store;
        MusicService service;
        MusicManager manager;
        GameAudioInfo active;
        bool completed, restoring, failed, pendingRestore;
        float nextSave, retryAfter, restoreDeadline;
        ConfigEntry<bool> savingEnabled, resume;
        ProgressRuntimeHost host;
        void Awake()
        {
            Instance = this;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            savingEnabled = Config.Bind("Progress", "Enabled", true, "保存六个平台音频的播放进度。停用不会删除记录。");
            resume = Config.Bind("Progress", "ResumeOnPlay", true, "再次点播相同音频时恢复进度；不自动登录、导入歌单或开始播放。");
            store = new ProgressStore(Path.Combine(Paths.ConfigPath, "ChillAudioProgress", "progress.json"), delegate(string m) { Logger.LogWarning(m); });
            GameObject runtime = new GameObject("ChillAudioProgress.Runtime");
            runtime.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runtime);
            host = runtime.AddComponent<ProgressRuntimeHost>(); host.Owner = this;
            harmony = new Harmony("local.chill.audioprogress");
            try
            {
                foreach (ConstructorInfo ctor in typeof(MusicService).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(Plugin), "CaptureService"));
                foreach (string name in new[] { "PlayArugumentMusic", "PlayMusicInPlaylist", "PlayNextMusic", "PlayBackMusic", "Stop", "Dispose" })
                    harmony.Patch(AccessTools.Method(typeof(MusicService), name), prefix: new HarmonyMethod(typeof(Plugin), "BeforeChange"));
                harmony.Patch(AccessTools.PropertySetter(typeof(MusicService), "PlayingMusic"), postfix: new HarmonyMethod(typeof(Plugin), "AfterPlayingChanged"));
                harmony.Patch(AccessTools.Method(typeof(MusicService), "Pause"), postfix: new HarmonyMethod(typeof(Plugin), "SaveNow"));
                harmony.Patch(AccessTools.Method(typeof(MusicService), "SetMusicProgress"), postfix: new HarmonyMethod(typeof(Plugin), "AfterSeek"));
                // The game's completion callbacks run before repeat/automatic-next starts.
                int callbacks = 0;
                foreach (MethodInfo method in typeof(MusicService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                    if (method.ReturnType == typeof(void) && method.GetParameters().Length == 0 &&
                        (method.Name.StartsWith("<PlayArugumentMusic>b__", StringComparison.Ordinal) ||
                         method.Name.StartsWith("<PlayMusicInPlaylist>b__", StringComparison.Ordinal) ||
                         method.Name.StartsWith("<PlayNextMusic>b__", StringComparison.Ordinal)))
                    { harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin), "OnCompleted")); callbacks++; }
                if (callbacks != 3) throw new NotSupportedException("播放器完成回调与支持版本不同。");
                Logger.LogInfo("音频进度保存已启用，每 5 秒保存，支持六个平台再次点播续播。");
            }
            catch (Exception error)
            {
                failed = true; harmony.UnpatchSelf();
                Logger.LogError("音频进度插件未启用，游戏播放保持原样：" + error.GetType().Name);
            }
        }
        AudioSource Source()
        {
            if (service == null || service.PlayingMusic == null || service.PlayingMusic.AudioClip == null) return null;
            if (manager == null)
            {
                MusicManager[] managers = Resources.FindObjectsOfTypeAll<MusicManager>();
                if (managers.Length == 0) return null;
                manager = managers[0];
            }
            AudioPlayer player = manager.GetPlayer(service.PlayingMusic.AudioClip);
            if (player == null || player.AudioSource == null || player.AudioSource.clip != service.PlayingMusic.AudioClip) return null;
            return player.AudioSource;
        }
        void Guard(Action action)
        {
            if (failed || !savingEnabled.Value || Time.realtimeSinceStartup < retryAfter) return;
            try { action(); }
            catch (Exception error) { retryAfter = Time.realtimeSinceStartup + 10; Logger.LogWarning("音频进度暂不可用：" + error.GetType().Name); }
        }
        void Begin()
        {
            active = service == null ? null : service.PlayingMusic; completed = false;
            pendingRestore = active != null && ProgressStore.Supported(active.UUID) && resume.Value;
            restoreDeadline = Time.realtimeSinceStartup + 10;
            TryRestore();
        }
        void TryRestore()
        {
            if (!pendingRestore) return;
            AudioSource source = Source();
            if (source == null)
            {
                if (Time.realtimeSinceStartup >= restoreDeadline) pendingRestore = false;
                return;
            }
            pendingRestore = false;
            double seconds = store.Resume(active.UUID, source.clip.length);
            if (seconds <= 0) return;
            // Use seconds directly: the game's seek API rejects ratios <= 0.001,
            // which otherwise loses the first seconds of a several-hour episode.
            restoring = true;
            try { source.time = (float)seconds; }
            finally { restoring = false; }
            Logger.LogInfo("已恢复音频进度 " + TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"));
        }
        void Sample()
        {
            if (service == null || active == null || completed || pendingRestore || service.PlayingMusic != active || !ProgressStore.Supported(active.UUID)) return;
            AudioSource source = Source();
            if (source == null) return;
            // A stopped AudioSource resets time to zero. Do not erase its last bookmark.
            if (!source.isPlaying && source.time < 0.01f) return;
            store.Record(active.UUID, source.time, source.clip.length);
        }
        static void CaptureService(MusicService __instance) { if (Instance != null) Instance.service = __instance; }
        static void BeforeChange(MusicService __instance) { SaveNow(__instance); }
        static void SaveNow(MusicService __instance)
        {
            if (Instance == null) return;
            Instance.Guard(delegate { Instance.service = __instance; Instance.Sample(); Instance.store.Flush(); });
        }
        static void AfterPlayingChanged(MusicService __instance)
        {
            if (Instance == null) return;
            Instance.Guard(delegate { Instance.service = __instance; Instance.Begin(); });
        }
        static void AfterSeek(MusicService __instance)
        {
            if (Instance == null || Instance.restoring) return;
            Instance.Guard(delegate { Instance.service = __instance; Instance.completed = false; Instance.pendingRestore = false; Instance.Sample(); Instance.store.Flush(); });
        }
        static void OnCompleted(MusicService __instance)
        {
            if (Instance == null) return;
            Instance.Guard(delegate
            {
                Instance.service = __instance;
                if (__instance.PlayingMusic != null) Instance.store.Complete(__instance.PlayingMusic.UUID);
                Instance.completed = true; Instance.pendingRestore = false; Instance.store.Flush();
            });
        }
        internal void Tick()
        {
            Guard(delegate
            {
                if (service != null && service.PlayingMusic != active) Begin();
                TryRestore();
                if (Time.realtimeSinceStartup >= nextSave)
                { nextSave = Time.realtimeSinceStartup + 5; Sample(); store.Flush(); }
            });
        }
        internal void FlushCurrent() { if (service != null) SaveNow(service); }
        void OnDestroy()
        {
            if (service != null) SaveNow(service);
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
            if (host != null) Destroy(host.gameObject);
        }
    }
    public sealed class ProgressRuntimeHost : MonoBehaviour
    {
        internal Plugin Owner;
        void Update() { if (Owner != null) Owner.Tick(); }
        void OnApplicationFocus(bool focus) { if (!focus && Owner != null) Owner.FlushCurrent(); }
        void OnApplicationPause(bool pause) { if (pause && Owner != null) Owner.FlushCurrent(); }
        void OnApplicationQuit() { if (Owner != null) Owner.FlushCurrent(); }
    }
}
