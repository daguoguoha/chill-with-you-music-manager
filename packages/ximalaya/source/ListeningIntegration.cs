// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Bulbul;
using HarmonyLib;
using KanKikuchi.AudioManager;
using UnityEngine;

namespace ChillXimalayaPlaylist
{
    public sealed partial class Plugin
    {
        static long UtcMilliseconds { get { return (long)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalMilliseconds; } }
        ConfigEntry<bool> reportListening;
        readonly ListeningTracker listening = new ListeningTracker();
        readonly Dictionary<string, string> listeningSources = new Dictionary<string, string>();
        Song listeningSong;
        string listeningPlaylist;
        long listeningPosition, listeningStartedAt;
        int listeningLastHeartbeat;
        readonly Queue<ListeningEvent> listeningQueue = new Queue<ListeningEvent>();
        readonly object listeningGate = new object();
        Task listeningWorker;
        MusicManager listeningManager;
        GameAudioInfo listeningInfo;
        int listeningEpoch;
        bool listeningClosed, listeningSwitchingAccount, listeningSending;
        volatile bool listeningAllowed;
        double listeningCooldown;
        string listeningMessage = "按实际播放计时";
        sealed class ListeningEvent { public Song Song; public string Playlist; public long Seconds, Position, StartedAt, EndedAt; public int Kind, Epoch; }
        static double ListeningClock { get { return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency; } }

        void SetupListening()
        {
            reportListening = Config.Bind("Listening", "ReportActualPlayback", true, "将游戏中实际播放的喜马拉雅歌曲时长上报至当前喜马拉雅账号；暂停、加载和跳过的进度不计时。喜马拉雅决定是否计入统计。关闭后停止采集和待发送记录。");
            listeningAllowed = reportListening.Value;
            harmony.Patch(AccessTools.PropertySetter(typeof(MusicService), "PlayingMusic"), prefix: new HarmonyMethod(typeof(Plugin), "ListeningChanged"));
            foreach (string name in new[] { "Pause", "UnPause", "SetMusicProgress" })
                harmony.Patch(AccessTools.Method(typeof(MusicService), name), prefix: new HarmonyMethod(typeof(Plugin), "ListeningBeforeControl"), postfix: new HarmonyMethod(typeof(Plugin), "ListeningAfterControl"));
            Logger.LogInfo("真实播放记录上报已接入；不承诺平台统计结果。");
        }
        static void ListeningChanged()
        {
            if (Instance == null) return;
            Instance.ListeningTickSafe();
            Instance.FinishListening(2);
        }
        static void ListeningBeforeControl() { if (Instance != null) Instance.ListeningTickSafe(); }
        static void ListeningAfterControl() { if (Instance != null) Instance.listening.BreakContinuity(); }

        void ListeningTickSafe()
        {
            if (reportListening == null || listeningClosed) return;
            try { ListeningTick(); }
            catch (Exception) { listening.BreakContinuity(); listeningMessage = "暂时无法读取播放状态"; }
        }
        void ListeningTick()
        {
            if (reportListening.Value != listeningAllowed) ResetListening(reportListening.Value);
            if (!listeningAllowed || !ready || !logged || listeningSwitchingAccount)
            { listening.Clear(); listeningInfo = null; return; }
            MusicService service = NativeService;
            GameAudioInfo info = service == null ? null : service.PlayingMusic;
            if (info != listeningInfo)
            {
                FinishListening(2);
                listeningInfo = info;
                if (IsCloud(info) && info.AudioClip != null)
                {
                    Song song;
                    if (nativeSongs.TryGetValue(info.UUID, out song))
                    {
                        listeningSong = song;
                        listeningSources.TryGetValue(info.UUID, out listeningPlaylist);
                        listening.Begin(1, 0, info.AudioClip.length);
                        listeningStartedAt = 0; listeningLastHeartbeat = 0;
                    }
                }
            }
            if (listening.SongId == 0 || info == null || info.AudioClip == null) return;
            if (listeningManager == null)
            {
                MusicManager[] managers = Resources.FindObjectsOfTypeAll<MusicManager>();
                if (managers.Length > 0) listeningManager = managers[0];
            }
            AudioPlayer player = listeningManager == null ? null : listeningManager.GetPlayer(info.AudioClip);
            AudioSource source = player == null ? null : player.AudioSource;
            if (source == null || source.clip != info.AudioClip) { listening.BreakContinuity(); return; }
            listening.Sample(ListeningClock, source.time, source.isPlaying && !service.IsPause, Math.Abs(source.pitch));
            if (source.isPlaying || service.IsPause) listeningPosition = (long)Math.Max(0, source.time);
            if (!listening.Started && listening.Seconds >= 1)
            {
                listening.Started = true;
                listeningStartedAt = UtcMilliseconds - listening.Seconds * 1000;
                EnqueueListening(0, 0);
            }
            if (listening.Started && listening.Seconds - listeningLastHeartbeat >= 20)
            { listeningLastHeartbeat = listening.Seconds; EnqueueListening(listening.Seconds, 4); }
            // At natural end the source can reset before the next track is assigned.
            // Preserve the accumulated time and emit the final record on that assignment.
        }
        void FinishListening(int kind)
        {
            if (listening.SongId != 0 && listening.Started && listening.Seconds >= 1)
                EnqueueListening(listening.Seconds, kind);
            listening.Clear(); listeningInfo = null;
        }
        void EnqueueListening(long seconds, int kind)
        {
            lock (listeningGate)
            {
                if (!listeningAllowed || listeningSwitchingAccount || ListeningClock < listeningCooldown) return;
                if (listeningQueue.Count >= 64) { listeningMessage = "队列已满，部分记录未发送"; return; }
                listeningQueue.Enqueue(new ListeningEvent { Song = listeningSong, Playlist = listeningPlaylist, Seconds = seconds, Position = listeningPosition, StartedAt = listeningStartedAt, EndedAt = UtcMilliseconds, Kind = kind, Epoch = listeningEpoch });
                if (!listeningSending)
                {
                    listeningSending = true;
                    listeningWorker = Task.Factory.StartNew(SendListeningQueue);
                }
            }
        }
        void SendListeningQueue()
        {
            while (true)
            {
                ListeningEvent item;
                lock (listeningGate)
                {
                    if (listeningQueue.Count == 0) { listeningSending = false; return; }
                    item = listeningQueue.Dequeue();
                }
                int code;
                try
                {
                    lock (nativeGate)
                    {
                        if (item.Epoch != listeningEpoch || !listeningAllowed) continue;
                        code = Bridge.ReportPlayback(item.Song, item.Playlist, item.Seconds, item.Position, item.StartedAt, item.EndedAt, item.Kind);
                    }
                }
                catch (Exception) { code = 0; }
                if (code == 200 || code == 202)
                {
                    listeningMessage = code == 202 ? "正在本地计时" : item.Kind == 0 ? "正在记录实际播放" : "已发送 " + item.Seconds + " 秒";
                    if (code == 200) Logger.LogInfo(item.Kind == 0 ? "听歌记录：开始播放已提交。" : "听歌记录：实际播放 " + item.Seconds + " 秒已提交。平台统计以喜马拉雅为准。");
                }
                else
                {
                    listeningMessage = code == 401 || code == 301 ? "登录已失效，请重新扫码" : "上报未完成，稍后再试";
                    Logger.LogWarning("听歌记录未提交（状态 " + code + "）；本条不自动重发，不影响播放。");
                    lock (listeningGate) { listeningQueue.Clear(); listeningCooldown = ListeningClock + 60; }
                }
            }
        }
        void ResetListening(bool enabled)
        {
            lock (listeningGate) { listeningEpoch++; listeningAllowed = enabled; listeningQueue.Clear(); }
            listening.Clear(); listeningInfo = null;
            listeningMessage = enabled ? "按实际播放计时" : "停止上报";
        }
        void ToggleListening()
        {
            reportListening.Value = !reportListening.Value;
            ResetListening(reportListening.Value); Config.Save();
        }
        void ResetListeningAccount()
        {
            ResetListening(listeningAllowed); listeningSwitchingAccount = true;
        }
        void OnApplicationQuit() { CloseListening(); }
        void CloseListening()
        {
            if (listeningClosed) return;
            ListeningTickSafe(); FinishListening(3); listeningClosed = true;
            Task worker;
            lock (listeningGate) worker = listeningWorker;
            // Bounded best effort. Never hold game shutdown for a network timeout.
            if (worker != null) { try { worker.Wait(1500); } catch (Exception) { } }
        }
    }
}
