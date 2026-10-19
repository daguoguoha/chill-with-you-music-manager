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
using QRCoder;
using UnityEngine;
using UnityEngine.Networking;

namespace ChillNeteasePlaylist
{
    [BepInPlugin("local.chill.neteaseplaylist", "网易云歌单", "1.4.2")]
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

        void Awake()
        {
            Instance = this;
            // This game changes scenes immediately after the BepInEx bootstrap.
            // Keep the plugin and its callbacks outside scene-owned objects.
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            GameObject runtime = new GameObject("ChillNetease.Runtime");
            runtime.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runtime);
            host = runtime.AddComponent<RuntimeHost>();
            host.Owner = this;
            volume = Config.Bind("Player", "Volume", 0.65f, "网易云音量，0 到 1。");
            shortcut = Config.Bind("Panel", "Shortcut", new KeyboardShortcut(KeyCode.F8), "显示或隐藏歌单面板。");
            showLauncher = Config.Bind("Panel", "ShowLauncher", true, "是否显示游戏内入口按钮。隐藏后仍可按 F8 打开面板。");
            audio = runtime.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = Mathf.Clamp01(volume.Value);
            string dir = Path.GetDirectoryName(Info.Location);
            if (Bridge.LoadLibraryEx(Path.Combine(dir, "ChillNetease.dll"), IntPtr.Zero, 8) == IntPtr.Zero)
            { status = "网易云桥接加载失败，请检查完整安装包。"; return; }
            try
            {
                harmony = new Harmony("local.chill.neteaseplaylist");
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
                    ready = (int)result == 1;
                    Logger.LogInfo("网易云桥接初始化完成：" + ready);
                    if (!ready) { status = "初始化失败，请检查 Mod 目录写入权限。"; return; }
                    status = "点击扫码登录，读取你自己的网易云歌单。";
                    Run(delegate { return Bridge.NeteaseRefreshLogin() == 1; }, delegate(object session)
                    { logged = (bool)session; if (logged) LoadAccount(); });
                });
                Logger.LogInfo("网易云歌单 Mod 已初始化；F8 显示面板。");
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
            if (firstTick) { firstTick = false; Logger.LogInfo("网易云运行对象 Update 已开始。"); }
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
                            object key = keyboardType.GetProperty("f8Key").GetValue(keyboard, null);
                            toggle = (bool)key.GetType().GetProperty("wasPressedThisFrame").GetValue(key, null);
                        }
                    }
                }
                catch { /* The panel remains visible even without a keyboard API. */ }
            }
            // Also support F8 when Unity's legacy input misses injected keys.
            bool keyDown = RuntimeHost.IsF8DownInGame();
            if (keyDown && !nativeKeyDown) toggle = true;
            nativeKeyDown = keyDown;
            if (toggle && Time.realtimeSinceStartup - lastToggle > 0.25f)
            { lastToggle = Time.realtimeSinceStartup; visible = !visible; Logger.LogInfo("F8 面板状态：" + visible); }
            if (job != null && job.IsCompleted)
            {
                Task<object> finished = job; Action<object> done = onDone;
                job = null; onDone = null; busy = false;
                if (finished.IsFaulted)
                { status = "请求未完成，请检查网易云网络和登录状态后重试。"; qrPolling = false; Logger.LogWarning("网易云请求失败：" + finished.Exception.GetBaseException().GetType().Name); }
                else if (!finished.IsCanceled) { try { done(finished.Result); } catch (Exception ex) { status = "处理响应失败，请重试。"; Logger.LogWarning(ex.GetType().Name); } }
            }
            if (ready && qrPolling && !busy && Time.realtimeSinceStartup >= nextPoll)
            {
                nextPoll = Time.realtimeSinceStartup + 3;
                Run(delegate { return JsonConvert.DeserializeObject<QrState>(Bridge.Take(Bridge.NeteaseQRCheckStatus())); }, delegate(object result)
                {
                    QrState qr = (QrState)result;
                    if (qr.statusCode == 803)
                    { qrPolling = false; logged = true; listeningSwitchingAccount = false; ClearQr(); LoadAccount(); }
                    else if (qr.statusCode == 800) { qrPolling = false; status = "二维码已过期，请重新生成。"; ClearQr(); }
                    else status = qr.statusCode == 802 ? "已扫码，请在手机网易云确认登录。" : "请使用手机网易云扫一扫。";
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
            Run(delegate { return JsonConvert.DeserializeObject<QrState>(Bridge.Take(Bridge.NeteaseQRGetKey())); }, delegate(object result)
            {
                QrState qr = (QrState)result;
                Uri loginUri;
                if (!Uri.TryCreate(qr.qrcodeUrl, UriKind.Absolute, out loginUri) || loginUri.Host != "music.163.com")
                    throw new InvalidOperationException("二维码地址无效。");
                using (QRCodeGenerator generator = new QRCodeGenerator())
                using (QRCodeData data = generator.CreateQrCode(qr.qrcodeUrl, QRCodeGenerator.ECCLevel.M))
                using (PngByteQRCode code = new PngByteQRCode(data))
                {
                    qrImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    qrImage.LoadImage(code.GetGraphic(7));
                    qrImage.filterMode = FilterMode.Point;
                }
                qrPolling = true; nextPoll = Time.realtimeSinceStartup + 3;
                status = "请用手机网易云扫码，并由你确认登录。";
            });
        }
        void LoadAccount()
        {
            Run(delegate { return JsonConvert.DeserializeObject<User>(Bridge.Take(Bridge.NeteaseGetUserInfo())); }, delegate(object result)
            { nickname = ((User)result).nickname; RefreshPlaylists(); });
        }
        void RefreshPlaylists()
        {
            qrPolling = false;
            status = "正在读取我的歌单…";
            Run(delegate
            {
                List<Playlist> all = new List<Playlist>();
                for (int offset = 0; offset < 10000; offset += 100)
                {
                    PlaylistPage page = JsonConvert.DeserializeObject<PlaylistPage>(Bridge.Take(Bridge.NeteaseGetUserPlaylists(100, offset)));
                    if (page.playlists != null) all.AddRange(page.playlists);
                    if (!page.hasMore || page.playlists == null || page.playlists.Count == 0) break;
                }
                return all;
            }, delegate(object result)
            { playlists = (List<Playlist>)result; status = "已读取 " + playlists.Count + " 个歌单，选择左侧歌单。"; });
        }
        void OpenPlaylist(long id, string title)
        {
            status = "正在读取歌单「" + title + "」…";
            Run(delegate { return JsonConvert.DeserializeObject<List<Song>>(Bridge.Take(Bridge.NeteaseGetPlaylistSongs(id, 1))) ?? new List<Song>(); }, delegate(object result)
            { songs = (List<Song>)result; selectedPlaylistId = id; selectedPlaylistTitle = title; query = ""; songScroll = Vector2.zero; showImport = false; status = title + " · " + songs.Count + " 首。点击歌曲播放。"; RegisterNativePlaylist(); });
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
            Logger.LogInfo("网易云插件对象已销毁。");
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

