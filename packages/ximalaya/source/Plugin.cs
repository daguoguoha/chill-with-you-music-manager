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

namespace ChillXimalayaPlaylist
{
    [BepInPlugin("local.chill.ximalayaplaylist", "喜马拉雅歌单", "1.2.0")]
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
        bool visible = false, busy, ready, logged, qrPolling, paused, playing, destroying, legacyInput = true;
        string status = "正在初始化…", nickname = "", query = "", playlistLink = "", currentName = "";
        List<Playlist> playlists = new List<Playlist>();
        List<Song> songs = new List<Song>();
        Vector2 playlistScroll, songScroll;
        Texture2D qrImage;
        RuntimeHost host;
        bool firstTick = true, firstGui = true, nativeKeyDown;
        int generation, current = -1;
        Song[] queue = new Song[0];
        Task<object> job;
        Action<object> onDone;
        float nextPoll, lastToggle = -1;
        string loginType = "ximalaya";

        void Awake()
        {
            Instance = this;
            // This game changes scenes immediately after the BepInEx bootstrap.
            // Keep the plugin and its callbacks outside scene-owned objects.
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            GameObject runtime = new GameObject("ChillXimalaya.Runtime");
            runtime.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runtime);
            host = runtime.AddComponent<RuntimeHost>();
            host.Owner = this;
            volume = Config.Bind("Player", "Volume", 0.65f, "喜马拉雅音量，0 到 1。");
            shortcut = Config.Bind("Panel", "Shortcut", new KeyboardShortcut(KeyCode.F7), "显示或隐藏歌单面板。");
            showLauncher = Config.Bind("Panel", "ShowLauncher", true, "是否显示游戏内入口按钮。隐藏后仍可按 F7 打开面板。");
            audio = runtime.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = Mathf.Clamp01(volume.Value);
            string dir = Path.GetDirectoryName(Info.Location);
            try
            {
                harmony = new Harmony("local.chill.ximalayaplaylist");
                Type type = AccessTools.TypeByName("MusicService");
                if (type == null) throw new InvalidOperationException("找不到游戏 MusicService。");
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(Plugin), "CaptureService"));
                SetupNative();
                SetupLyrics();
                SetupPlaybackSession();
                SetupListening();
                string data = Path.Combine(dir, "account");
                Run(delegate { return Bridge.Init(data); }, delegate(object result)
                {
                    ready = (int)result == 0;
                    Logger.LogInfo("喜马拉雅桥接初始化完成：" + ready);
                    if (!ready) { status = "初始化失败，请检查 Mod 目录写入权限。"; return; }
                    status = "使用喜马拉雅 App 扫码，或直接导入公开专辑。";
                    Run(delegate { return Bridge.IsLoggedIn; }, delegate(object session)
                    { logged = (bool)session; if (logged) LoadAccount(); });
                });
                Logger.LogInfo("喜马拉雅歌单 Mod 已初始化；F7 显示面板。");
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
            ListeningTickSafe();
            try { NativeTick(); } catch (Exception ex) { nativeNextCheck = Time.realtimeSinceStartup + 10; Logger.LogWarning("原生歌单界面：" + ex); }
            if (firstTick) { firstTick = false; Logger.LogInfo("喜马拉雅运行对象 Update 已开始。"); }
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
                            object key = keyboardType.GetProperty("f7Key").GetValue(keyboard, null);
                            toggle = (bool)key.GetType().GetProperty("wasPressedThisFrame").GetValue(key, null);
                        }
                    }
                }
                catch { /* The panel remains visible even without a keyboard API. */ }
            }
            // Also support F7 when Unity's legacy input misses injected keys.
            bool keyDown = RuntimeHost.IsF7DownInGame();
            if (keyDown && !nativeKeyDown) toggle = true;
            nativeKeyDown = keyDown;
            if (toggle && Time.realtimeSinceStartup - lastToggle > 0.25f)
            { lastToggle = Time.realtimeSinceStartup; visible = !visible; Logger.LogInfo("F7 面板状态：" + visible); }
            if (job != null && job.IsCompleted)
            {
                Task<object> finished = job; Action<object> done = onDone;
                job = null; onDone = null; busy = false;
                if (finished.IsFaulted)
                { status = "请求未完成：" + finished.Exception.GetBaseException().Message; qrPolling = false; Logger.LogWarning("喜马拉雅请求失败：" + finished.Exception.GetBaseException().GetType().Name); }
                else if (!finished.IsCanceled) { try { done(finished.Result); } catch (Exception ex) { status = "处理响应失败，请重试。"; Logger.LogWarning(ex.GetType().Name); } }
            }
            if (ready && qrPolling && !busy && Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 3;
                Run(delegate { return JsonConvert.DeserializeObject<QrState>(Bridge.CheckQr()); }, delegate(object result)
                {
                    QrState qr = (QrState)result;
                    if (qr.code == 0 && Bridge.IsLoggedIn)
                    { qrPolling = false; logged = true; listeningSwitchingAccount = false; ClearQr(); LoadAccount(); }
                    else if (qr.code == 65) { qrPolling = false; status = "二维码已过期，请重新生成。"; ClearQr(); }
                    else if (qr.code != 66 && qr.code != 67) { qrPolling = false; status = "登录未完成，请重新扫码。"; ClearQr(); }
                    else status = qr.code == 67 ? "已扫码，请在手机上确认登录。" : "请使用喜马拉雅 App 扫一扫。";
                });
            }
            if (playing && !paused && !audio.isPlaying && audio.clip != null && !busy && request == null)
            { playing = false; Next(1); }
        }
        void ClearQr() { if (qrImage != null) Destroy(qrImage); qrImage = null; }
        void Login()
        {
            ResetPlaybackSessionAccount();
            ResetListeningAccount();
            qrPolling = false; ClearQr();
            status = "正在获取登录二维码，连接失败时自动重试一次…";
            Run(delegate { Bridge.CancelQr(); return Bridge.QrImage(loginType); }, delegate(object result)
            {
                byte[] png = Convert.FromBase64String((string)result);
                if (png.Length > 1024 * 1024) throw new InvalidOperationException("二维码图片过大。");
                qrImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!qrImage.LoadImage(png)) { ClearQr(); throw new InvalidOperationException("二维码图片无效。"); }
                qrImage.filterMode = FilterMode.Point;
                qrPolling = true; nextPoll = Time.realtimeSinceStartup + 3;
                status = "请使用喜马拉雅 App 扫码，并在手机确认登录。";
            });
        }
        void LoadAccount()
        {
            Run(delegate { return JsonConvert.DeserializeObject<User>(Bridge.UserInfo()); }, delegate(object result)
            { nickname = ((User)result).nickname; RefreshPlaylists(); });
        }
        void RefreshPlaylists()
        {
            qrPolling = false;
            status = "正在读取我的订阅…";
            Run(delegate
            {
                UserPlaylists page = JsonConvert.DeserializeObject<UserPlaylists>(Bridge.UserPlaylists());
                return Rules.MergePlaylists(page);
            }, delegate(object result)
            { playlists = (List<Playlist>)result; nativeMenuVersion = -1; status = "已读取 " + playlists.Count + " 个歌单，选择左侧专辑。"; });
        }
        void OpenPlaylist(string id, string title)
        {
            status = "正在读取专辑「" + title + "」…";
            Run(delegate { return Bridge.Playlist(id); }, delegate(object result)
            { if (title == "导入的专辑") title = Bridge.LastPlaylistTitle; songs = Rules.UniqueSongs((List<Song>)result); selectedPlaylistId = id; selectedPlaylistTitle = title; query = ""; songScroll = Vector2.zero; showImport = false;
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
        void OnDestroy()
        {
            ClosePlaybackSession();
            CloseLyrics();
            CloseListening();
            Logger.LogInfo("喜马拉雅插件对象已销毁。");
            destroying = true; qrPolling = false;
            StopExternal(); ClearQr();
            foreach (var item in nativeClips) ReleaseNativeClip(item);
            foreach (var button in nativeButtons) if (button != null) Destroy(button);
            DisposeUI();
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
            if (host != null) Destroy(host.gameObject);
        }
    }
}

