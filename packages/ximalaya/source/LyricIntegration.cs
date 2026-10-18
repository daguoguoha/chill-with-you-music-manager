// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Bulbul;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace ChillXimalayaPlaylist
{
    public sealed partial class Plugin
    {
        const string LyricProvider = "ximalaya";
        ConfigEntry<bool> lyricsEnabled;
        ConfigEntry<float> lyricsOffset, lyricsBottom, lyricsX, lyricsY;
        ConfigEntry<int> lyricsSize;
        ConfigEntry<string> lyricsColor;
        string lyricsDirectory, lyricsMessage = "播放一首歌曲后可显示歌词";
        string lyricsFile = "", lyricsPaste = "", lyricsImportKey;
        LyricTrack lyricTrack;
        LyricDocument lyricDocument = new LyricDocument();
        Task<LyricResult> lyricTask;
        CancellationTokenSource lyricCancellation;
        int lyricEpoch, lyricTaskEpoch;
        bool lyricNeedsLoad, lyricRefresh, lyricsSettingsOpen, lyricsClosed, lyricImport;
        float lyricNextPoll;
        double lyricPosition;
        MusicManager lyricManager;
        bool lyricVWasDown, lyricTyping;

        void SetupLyrics()
        {
            lyricsEnabled = Config.Bind("Lyrics", "Enabled", true, "显示游戏画面下方的同步歌词，可在歌词设置中独立隐藏。");
            lyricsOffset = Config.Bind("Lyrics", "OffsetSeconds", 0f, new ConfigDescription("歌词同步修正，正数提前，负数延后。", new AcceptableValueRange<float>(-15f, 15f)));
            lyricsBottom = Config.Bind("Lyrics", "BottomMargin", 110f, new ConfigDescription("歌词底边距离屏幕底部的基准像素，随分辨率缩放。", new AcceptableValueRange<float>(30f, 360f)));
            lyricsX = Config.Bind("Lyrics", "PositionX", -1f, new ConfigDescription("拖动后保存的横向位置比例；-1 使用默认居中。", new AcceptableValueRange<float>(-1f, 1f)));
            lyricsY = Config.Bind("Lyrics", "PositionY", -1f, new ConfigDescription("拖动后保存的纵向位置比例；-1 使用默认底部位置。", new AcceptableValueRange<float>(-1f, 1f)));
            lyricsSize = Config.Bind("Lyrics", "FontSize", 22, new ConfigDescription("歌词基准字号，随分辨率缩放。", new AcceptableValueRange<int>(14, 34)));
            lyricsColor = Config.Bind("Lyrics", "ColorHex", "#F0EBFA", "歌词颜色，使用 #RRGGBB 格式，可在歌词设置中选择预设或输入自定义颜色。");
            lyricsDirectory = Path.Combine(Paths.ConfigPath, "ChillLyrics", LyricProvider);
            Logger.LogInfo("底部同步歌词已接入；歌词显示与播放记录开关分别控制。");
        }
        void LyricsTickSafe()
        {
            if (lyricsClosed || lyricsEnabled == null) return;
            try { LyricsHotkey(); LyricsTick(); }
            catch (Exception) { lyricsMessage = "暂时无法读取歌词 · 不影响音乐播放"; }
        }
        void LyricsHotkey()
        {
            bool down = RuntimeHost.IsVDownInGame();
            bool pressed = down && !lyricVWasDown;
            lyricVWasDown = down;
            if (!pressed || lyricTyping || RuntimeHost.HasLyricShortcutModifier()) return;
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            var selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                var input = selected.GetComponent<TMPro.TMP_InputField>();
                if (input != null && input.isFocused) return;
                var legacy = selected.GetComponent<UnityEngine.UI.InputField>();
                if (legacy != null && legacy.isFocused) return;
            }
            var service = NativeService;
            if (service != null && IsCloud(service.PlayingMusic)) ToggleLyrics();
        }
        void CancelLyricLoad()
        {
            lyricEpoch++;
            if (lyricCancellation != null) { try { lyricCancellation.Cancel(); } catch (ObjectDisposedException) { } }
        }
        void LyricsTick()
        {
            if (Time.realtimeSinceStartup < lyricNextPoll) return;
            lyricNextPoll = Time.realtimeSinceStartup + 0.08f;
            MusicService service = NativeService;
            GameAudioInfo info = service == null ? null : service.PlayingMusic;
            string key = IsCloud(info) ? info.UUID : null;
            if ((lyricTrack == null ? null : lyricTrack.Key) != key)
            {
                CancelLyricLoad();
                lyricTrack = null; lyricDocument = new LyricDocument(); lyricPosition = 0;
                lyricsFile = ""; lyricsPaste = ""; lyricsImportKey = null; lyricImport = false;
                lyricNeedsLoad = key != null; lyricRefresh = false;
                lyricsMessage = key == null ? "播放本平台音频后可导入歌词" : "正在读取歌词…";
                Song song;
                if (key != null && nativeSongs.TryGetValue(key, out song))
                    lyricTrack = new LyricTrack { Key = key, Title = song.name, Id = Convert.ToString(song.id, CultureInfo.InvariantCulture), Hash = null, LocalPath = null, Duration = song.duration };
            }
            if (lyricTask != null && lyricTask.IsCompleted)
            {
                Task<LyricResult> completed = lyricTask;
                int epoch = lyricTaskEpoch;
                lyricTask = null;
                if (lyricCancellation != null) { lyricCancellation.Dispose(); lyricCancellation = null; }
                if (completed.IsFaulted)
                {
                    // Observe every task exception, including stale/cancelled track requests.
                    Exception error = completed.Exception.GetBaseException();
                    if (epoch == lyricEpoch) lyricsMessage = error is OperationCanceledException ? "歌词读取已取消" : "歌词读取未完成 · 可导入 LRC 或重试";
                }
                else if (!completed.IsCanceled && epoch == lyricEpoch && lyricTrack != null)
                { lyricDocument = completed.Result.Document; lyricsMessage = completed.Result.Message; }
            }
            if (!lyricsEnabled.Value && !lyricsSettingsOpen)
            {
                if (lyricTask != null) { CancelLyricLoad(); lyricNeedsLoad = lyricTrack != null; }
                return;
            }
            if (lyricTask == null && lyricTrack != null && lyricNeedsLoad)
            {
                lyricNeedsLoad = false;
                LyricTrack requested = lyricTrack;
                bool refresh = lyricRefresh, importing = lyricImport;
                string file = lyricsFile, pasted = lyricsPaste;
                lyricImport = false; lyricRefresh = false;
                lyricTaskEpoch = lyricEpoch;
                lyricCancellation = new CancellationTokenSource();
                CancellationToken token = lyricCancellation.Token;
                lyricTask = Task.Factory.StartNew(delegate
                {
                    return importing ? LyricSource.Import(lyricsDirectory, requested, file, pasted, token) : LyricSource.Load(LyricProvider, lyricsDirectory, requested, refresh, token);
                }, token);
            }
            if (info == null || info.AudioClip == null || !IsCloud(info)) return;
            if (lyricManager == null)
            { var managers = Resources.FindObjectsOfTypeAll<MusicManager>(); if (managers.Length > 0) lyricManager = managers[0]; }
            AudioPlayer player = lyricManager == null ? null : lyricManager.GetPlayer(info.AudioClip);
            AudioSource source = player == null ? null : player.AudioSource;
            if (source != null && source.clip == info.AudioClip) lyricPosition = source.time;
        }
        void ToggleLyrics()
        {
            FlushLyricDrag();
            lyricsEnabled.Value = !lyricsEnabled.Value;
            if (!lyricsEnabled.Value) CancelLyricLoad();
            else if (lyricDocument.Lines.Count == 0) lyricNeedsLoad = lyricTrack != null;
            Config.Save();
        }
        void RequestLyrics(bool import)
        {
            if (lyricTrack == null) { lyricsMessage = "请先播放本平台的一首音频。"; return; }
            if (import && lyricsImportKey != lyricTrack.Key) { lyricsMessage = "曲目已切换，请重新确认要导入的歌词。"; return; }
            CancelLyricLoad(); lyricNeedsLoad = true; lyricImport = import; lyricRefresh = !import;
            if (!import) lyricDocument = new LyricDocument();
            lyricsMessage = import ? "正在导入歌词…" : "正在重新获取歌词…";
        }
        void CloseLyrics()
        {
            FlushLyricDrag();
            lyricsClosed = true; CancelLyricLoad();
            // Network cancellation aborts the request. Never wait on it during game exit.
        }
    }
}
