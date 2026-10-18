// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ChillSaltPlayerPlaylist
{
    [BepInPlugin("local.chill.saltplayerplaylist", "椒盐音乐歌单", "1.1.0")]
    [BepInProcess("Chill With You.exe")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static object MusicService;
        Harmony harmony;
        AudioSource audio;
        UnityWebRequest request;
        ConfigEntry<float> volume;
        ConfigEntry<bool> showLauncher;
        ConfigEntry<KeyboardShortcut> shortcut;
        Rect panel = new Rect(170, 70, 1000, 730);
        bool visible = false, busy, ready, paused, playing, destroying, legacyInput = true;
        string status = "正在初始化…", query = "", playlistLink = "", currentName = "";
        List<Playlist> playlists = new List<Playlist>();
        List<Song> songs = new List<Song>();
        Vector2 playlistScroll, songScroll;
        RuntimeHost host;
        bool firstTick = true, firstGui = true, nativeKeyDown;
        int generation, current = -1;
        Song[] queue = new Song[0];
        Task<object> job;
        Action<object> onDone;
        float lastToggle = -1;

        void Awake()
        {
            Instance = this;
            // This game changes scenes immediately after the BepInEx bootstrap.
            // Keep the plugin and its callbacks outside scene-owned objects.
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            GameObject runtime = new GameObject("ChillSaltPlayer.Runtime");
            runtime.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runtime);
            host = runtime.AddComponent<RuntimeHost>();
            host.Owner = this;
            volume = Config.Bind("Player", "Volume", 0.65f, "椒盐音乐音量，0 到 1。");
            shortcut = Config.Bind("Panel", "Shortcut", new KeyboardShortcut(KeyCode.F6), "显示或隐藏歌单面板。");
            showLauncher = Config.Bind("Panel", "ShowLauncher", true, "是否显示游戏内入口按钮。隐藏后仍可按 F6 打开面板。");
            audio = runtime.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = Mathf.Clamp01(volume.Value);
            string dir = Path.GetDirectoryName(Info.Location);
            try
            {
                harmony = new Harmony("local.chill.saltplayerplaylist");
                Type type = AccessTools.TypeByName("MusicService");
                if (type == null) throw new InvalidOperationException("找不到游戏 MusicService。");
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(Plugin), "CaptureService"));
                SetupNative();
                SetupLyrics();
                SetupPlaybackSession();
                string data = Path.Combine(dir, "library");
                Run(delegate { return Bridge.Init(data); }, delegate(object result)
                {
                    ready = (int)result == 0;
                    Logger.LogInfo("椒盐音乐桥接初始化完成：" + ready);
                    if (!ready) { status = "初始化失败，请检查 Mod 目录写入权限。"; return; }
                    musicRoot = Bridge.LastRoot; RefreshPlaylists();
                });
                Logger.LogInfo("椒盐音乐歌单 Mod 已初始化；F6 显示面板。");
            }
            catch (Exception ex) { status = "当前游戏版本初始化失败：" + ex.GetType().Name; Logger.LogError(status); }
        }
        static void CaptureService(object __instance) { MusicService = __instance; }
        static void BeforeOriginalPlayback() { if (Instance != null) Instance.StopExternal(); }
        void Run(Func<object> action, Action<object> done)
        {
            if (busy || destroying) return;
            busy = true; onDone = done;
            job = Task.Factory.StartNew(delegate { lock (nativeGate) { return action(); } });
        }
        internal void Tick()
        {
            LyricsTickSafe();
            PlaybackSessionTickSafe();
            try { NativeTick(); } catch (Exception ex) { nativeNextCheck = Time.realtimeSinceStartup + 10; Logger.LogWarning("原生歌单界面：" + ex); }
            if (firstTick) { firstTick = false; Logger.LogInfo("椒盐音乐运行对象 Update 已开始。"); }
            // Keep async completion working if the game uses only the new Input System.
            bool toggle = false;
            if (legacyInput)
            {
                try { toggle = shortcut.Value.IsDown(); }
                catch (InvalidOperationException) { legacyInput = false; }
            }
            if (!legacyInput)
            {
                try
                {
                    Type keyboardType = AccessTools.TypeByName("UnityEngine.InputSystem.Keyboard");
                    if (keyboardType != null)
                    {
                        object keyboard = keyboardType.GetProperty("current").GetValue(null, null);
                        if (keyboard != null)
                        {
                            object key = keyboardType.GetProperty("f6Key").GetValue(keyboard, null);
                            toggle = (bool)key.GetType().GetProperty("wasPressedThisFrame").GetValue(key, null);
                        }
                    }
                }
                catch { /* The panel remains visible even without a keyboard API. */ }
            }
            // Also support F6 when Unity's legacy input misses injected keys.
            bool keyDown = RuntimeHost.IsF6DownInGame();
            if (keyDown && !nativeKeyDown) toggle = true;
            nativeKeyDown = keyDown;
            if (toggle && Time.realtimeSinceStartup - lastToggle > 0.25f)
            { lastToggle = Time.realtimeSinceStartup; visible = !visible; Logger.LogInfo("F6 面板状态：" + visible); }
            if (job != null && job.IsCompleted)
            {
                Task<object> finished = job; Action<object> done = onDone;
                job = null; onDone = null; busy = false;
                if (finished.IsFaulted)
                { status = "操作未完成：" + finished.Exception.GetBaseException().Message; importReport = Bridge.LastImportDetails; Logger.LogWarning("椒盐音乐请求失败：" + finished.Exception.GetBaseException().GetType().Name); }
                else if (!finished.IsCanceled) { try { done(finished.Result); } catch (Exception ex) { status = "处理响应失败，请重试。"; Logger.LogWarning(ex.GetType().Name); } }
            }
            if (playing && !paused && !audio.isPlaying && audio.clip != null && !busy && request == null)
            { playing = false; Next(1); }
        }
        void RefreshPlaylists()
        {
            Run(delegate { return Bridge.Lists(); }, delegate(object result)
            { playlists = (List<Playlist>)result; nativeMenuVersion = -1; status = "已保存 " + playlists.Count + " 个本地歌单；可导入歌单或音乐文件夹。"; });
        }
        void OpenPlaylist(string id, string title)
        {
            status = "正在读取专辑「" + title + "」…";
            Run(delegate { return Bridge.Playlist(id); }, delegate(object result)
            { songs = Rules.UniqueSongs((List<Song>)result); selectedPlaylistId = id; selectedPlaylistTitle = title; query = ""; songScroll = Vector2.zero; showImport = false;
                if (!string.IsNullOrEmpty(id) && !playlists.Exists(p => p.id == id)) { playlists.Add(new Playlist { id = id, name = title, songCount = songs.Count }); nativeMenuVersion = -1; } status = title + " · " + songs.Count + " 集。点击声音播放。"; RegisterNativePlaylist(); });
        }
        void Play(Song[] list, int index, bool auto)
        {
            NativePlayFromPanel(list, index);
        }
        void Next(int direction)
        {
            if (NativeService != null) NativeService.PlayNextMusic(direction, (Bulbul.MusicChangeKind)1).Forget();
        }
        internal void StopExternal()
        {
            generation++; nativeLoadVersion++; playing = paused = false;
            if (request != null) { request.Abort(); request = null; }
            if (audio != null)
            { audio.Stop(); AudioClip clip = audio.clip; audio.clip = null; if (clip != null) Destroy(clip); }
        }
        void ResumeOriginal()
        {
            nativeLoadVersion++;
            if (NativeService != null && Bulbul.SaveDataManager.HasInstance)
            {
                Bulbul.SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = Bulbul.AudioTag.All;
                if (NativeService.CurrentPlayList.Count > 0) NativeService.PlayMusicInPlaylist(0);
            }
            visible = false;
        }
        void OnApplicationQuit() { ClosePlaybackSession(); }
        void OnDestroy()
        {
            ClosePlaybackSession();
            CloseLyrics();
            Logger.LogInfo("椒盐音乐插件对象已销毁。");
            destroying = true;
            FilePicker.Cancel();
            StopExternal();
            foreach (var item in nativeClips) ReleaseNativeClip(item);
            foreach (var button in nativeButtons) if (button != null) Destroy(button);
            DisposeUI();
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
            if (host != null) Destroy(host.gameObject);
        }
    }
}

