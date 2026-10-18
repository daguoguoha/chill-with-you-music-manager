// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Bulbul;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ChillXimalayaPlaylist
{
    public sealed partial class Plugin
    {
        ConfigEntry<bool> restorePlayback;
        string sessionPath, sessionKey;
        Task<PlaybackSession> sessionReading;
        Task sessionWriting;
        readonly object sessionWriteGate = new object();
        PlaybackSession sessionRestoring;
        bool sessionStartupDone, sessionApplying, sessionClosed, sessionHasPlayed, sessionReadLogged;
        float sessionNextSave, sessionDeadline, sessionSourceReadyAt = -1;
        float sessionSeekAt;
        bool sessionSeekRequested, sessionWasMuted;
        AudioSource sessionMutedSource;

        void SetupPlaybackSession()
        {
            restorePlayback = Config.Bind("Resume", "RestoreLastSession", true, "启动时恢复上次使用平台的歌单、曲目、进度与播放/暂停状态；需要原有登录、网络和音源权限。");
            sessionPath = Path.Combine(Paths.ConfigPath, "ChillPlaybackSession", "last.json");
            sessionReading = Task.Factory.StartNew(delegate { try { return PlaybackSessionStore.Read(sessionPath); } catch (Exception) { return null; } });
            harmony.Patch(AccessTools.PropertySetter(typeof(MusicService), "PlayingMusic"), prefix: new HarmonyMethod(typeof(Plugin), "SessionBeforeTrack"), postfix: new HarmonyMethod(typeof(Plugin), "SessionAfterTrack"));
            foreach (string name in new[] { "Pause", "UnPause", "SetMusicProgress" })
                harmony.Patch(AccessTools.Method(typeof(MusicService), name), postfix: new HarmonyMethod(typeof(Plugin), "SessionAfterControl"));
            Logger.LogInfo("上次播放会话恢复已接入；记录只保存在本机。");
        }
        static void SessionBeforeTrack() { if (Instance != null) Instance.SavePlaybackSession(true); }
        static void SessionAfterControl() { if (Instance != null) Instance.SavePlaybackSession(true); }
        static void SessionAfterTrack()
        {
            var owner = Instance;
            if (owner == null || owner.sessionClosed || owner.sessionApplying || !owner.sessionHasPlayed) return;
            var service = owner.NativeService;
            if (service == null || service.PlayingMusic == null || PlaybackSessionStoreProvider(service.PlayingMusic.UUID)) return;
            try
            {
                lock (owner.sessionWriteGate) PlaybackSessionStore.Write(owner.sessionPath, new PlaybackSession { Provider = "game", CurrentId = service.PlayingMusic.UUID, QueueJson = "[]", Duration = 1, UpdatedUtcTicks = DateTime.UtcNow.Ticks });
                owner.sessionHasPlayed = false;
            }
            catch (Exception) { }
        }
        void SessionManualSelection() { if (!sessionApplying) sessionStartupDone = true; }
        void ResetPlaybackSessionAccount() { ReleaseSessionMute(); sessionStartupDone = true; sessionApplying = false; sessionRestoring = null; }
        void PlaybackSessionTickSafe()
        {
            if (restorePlayback == null || sessionClosed) return;
            try
            {
                if (!sessionStartupDone && sessionReading != null && sessionReading.IsCompleted)
                {
                    if (!sessionReadLogged) { sessionReadLogged = true; Logger.LogInfo("续播记录读取：" + (sessionReading.IsFaulted ? "失败" : sessionReading.Result == null ? "无有效记录" : sessionReading.Result.Provider)); }
                    if (sessionReading.IsFaulted) { var observed = sessionReading.Exception; sessionStartupDone = true; }
                    else if (sessionReading.Result == null || sessionReading.Result.Provider != LyricProvider || !restorePlayback.Value) sessionStartupDone = true;
                    else if (ready && logged && !busy && NativeService != null && SaveDataManager.HasInstance && NativeService.AllMusicList != null && NativeService.AllMusicList.Count > 0 && NativeService.PlayingMusic != null)
                    {
                        GameAudioInfo now = NativeService.PlayingMusic;
                        if (now != null && PlaybackSessionStoreProvider(now.UUID)) sessionStartupDone = true;
                        else RestorePlaybackSession(sessionReading.Result);
                    }
                }
                if (sessionApplying) TryRestoreSessionPosition();
                if (Time.realtimeSinceStartup >= sessionNextSave) { sessionNextSave = Time.realtimeSinceStartup + 5; SavePlaybackSession(false); }
            }
            catch (Exception) { ReleaseSessionMute(); sessionStartupDone = true; sessionApplying = false; Logger.LogWarning("上次播放恢复未完成；可手动选择歌单，不影响播放器。"); }
        }
        static bool PlaybackSessionStoreProvider(string id)
        {
            return id != null && new[] { "netease:", "qqmusic:", "qq:", "kugou:", "bilibili:", "ximalaya:", "saltplayer:" }.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));
        }
        void RestorePlaybackSession(PlaybackSession record)
        {
            Logger.LogInfo("正在重建上次播放队列。");
            var saved = JsonConvert.DeserializeObject<List<Song>>(record.QueueJson);
            if (saved == null || saved.Count == 0 || saved.Count > 2000) { sessionStartupDone = true; return; }
            saved = saved.Where(s => s != null && !string.IsNullOrEmpty(Convert.ToString(s.id, CultureInfo.InvariantCulture))).ToList();
            int index = saved.FindIndex(s => Convert.ToString(s.id, CultureInfo.InvariantCulture) == record.CurrentId);
            if (index < 0) { sessionStartupDone = true; return; }
            selectedPlaylistId = record.PlaylistId;
            selectedPlaylistTitle = string.IsNullOrEmpty(record.PlaylistTitle) ? "上次播放" : record.PlaylistTitle;
            songs = saved; query = ""; songScroll = Vector2.zero;
            sessionStartupDone = true; sessionApplying = true; sessionRestoring = record;
            sessionSeekRequested = false; sessionSourceReadyAt = -1;
            sessionDeadline = Time.realtimeSinceStartup + 70;
            RegisterNativePlaylist(); NativePlayFromPanel(saved.ToArray(), index);
            status = "正在恢复上次播放…";
        }
        void TryRestoreSessionPosition()
        {
            var service = NativeService;
            if (sessionRestoring == null || service == null || Time.realtimeSinceStartup > sessionDeadline)
            { ReleaseSessionMute(); sessionApplying = false; return; }
            var info = service.PlayingMusic;
            if (!IsCloud(info) || info.UUID != CloudPrefix + sessionRestoring.CurrentId) return;
            var source = PlaybackSessionSource(info);
            if (source == null) return;
            if (sessionSourceReadyAt < 0)
            {
                sessionSourceReadyAt = Time.realtimeSinceStartup;
                if (sessionRestoring.Paused) { sessionMutedSource = source; sessionWasMuted = source.mute; source.mute = true; }
            }
            // Let the optional progress plugin apply its bookmark first, then use the exit snapshot.
            if (Time.realtimeSinceStartup - sessionSourceReadyAt < 0.5f) return;
            double seconds = PlaybackSessionStore.Position(sessionRestoring, source.clip.length);
            if (!sessionSeekRequested)
            { source.time = (float)seconds; sessionSeekRequested = true; sessionSeekAt = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - sessionSeekAt < 0.1f) return;
            // Unity may accept a seek then reset it on the next audio frame. Verify the real source before completing.
            if (Math.Abs(source.time - seconds) > (sessionRestoring.Paused ? 1 : 3))
            { source.time = (float)seconds; sessionSeekAt = Time.realtimeSinceStartup; return; }
            if (sessionRestoring.Paused && source.isPlaying)
            { service.Pause(); sessionSeekAt = Time.realtimeSinceStartup; return; }
            ReleaseSessionMute();
            sessionApplying = false; sessionRestoring = null;
            status = "已恢复上次曲目和播放进度。";
            Logger.LogInfo("上次播放会话已恢复，进度 " + TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"));
            SavePlaybackSession(true);
        }
        void ReleaseSessionMute()
        {
            if (sessionMutedSource != null) { try { sessionMutedSource.mute = sessionWasMuted; } catch (Exception) { } sessionMutedSource = null; }
        }
        AudioSource PlaybackSessionSource(GameAudioInfo info)
        {
            if (info == null || info.AudioClip == null) return null;
            if (lyricManager == null)
            { var managers = Resources.FindObjectsOfTypeAll<KanKikuchi.AudioManager.MusicManager>(); if (managers.Length > 0) lyricManager = managers[0]; }
            var player = lyricManager == null ? null : lyricManager.GetPlayer(info.AudioClip);
            var source = player == null ? null : player.AudioSource;
            return source != null && source.clip == info.AudioClip ? source : null;
        }
        void SavePlaybackSession(bool force)
        {
            if (sessionClosed || sessionApplying || sessionPath == null || restorePlayback == null || !restorePlayback.Value) return;
            try
            {
                var service = NativeService;
                var info = service == null ? null : service.PlayingMusic;
                if (!IsCloud(info)) return;
                var source = PlaybackSessionSource(info);
                if (source == null || source.clip.length <= 0 || (!source.isPlaying && source.time < 0.01f && !service.IsPause)) return;
                if (!force && sessionWriting != null && !sessionWriting.IsCompleted) return;
                Song currentSong;
                if (!nativeSongs.TryGetValue(info.UUID, out currentSong)) return;
                var list = new List<Song>(); var ids = new HashSet<string>();
                foreach (var item in service.CurrentPlayList)
                {
                    Song song;
                    if (IsCloud(item) && nativeSongs.TryGetValue(item.UUID, out song) && ids.Add(item.UUID)) list.Add(song);
                    if (list.Count >= 1999) break;
                }
                if (list.Count == 0 && queue != null)
                    foreach (var song in queue) { string key = CloudPrefix + Convert.ToString(song.id, CultureInfo.InvariantCulture); if (ids.Add(key)) list.Add(song); if (list.Count >= 1999) break; }
                if (ids.Add(info.UUID)) list.Insert(0, currentSong);
                var record = new PlaybackSession { Provider = LyricProvider, CurrentId = Convert.ToString(currentSong.id, CultureInfo.InvariantCulture), PlaylistId = Convert.ToString(selectedPlaylistId, CultureInfo.InvariantCulture), PlaylistTitle = selectedPlaylistTitle,
                    QueueJson = JsonConvert.SerializeObject(list), Seconds = source.time, Duration = source.clip.length, Paused = !source.isPlaying, UpdatedUtcTicks = DateTime.UtcNow.Ticks };
                sessionKey = info.UUID;
                sessionHasPlayed = true;
                if (force)
                { lock (sessionWriteGate) { PlaybackSessionStore.Write(sessionPath, record); } }
                else sessionWriting = Task.Factory.StartNew(delegate { try { lock (sessionWriteGate) { PlaybackSessionStore.Write(sessionPath, record); } } catch (Exception) { } });
            }
            catch (Exception) { Logger.LogWarning("播放会话暂时无法保存；音乐播放不受影响。"); }
        }
        void ClosePlaybackSession() { if (!sessionClosed) { ReleaseSessionMute(); SavePlaybackSession(true); sessionClosed = true; } }
        void OnApplicationFocus(bool focus) { if (!focus) SavePlaybackSession(true); }
    }
}
