// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using Bulbul;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ChillNeteasePlaylist
{
    public sealed partial class Plugin
    {
        readonly Dictionary<AudioClip, ProgressiveAudio> progressiveClips = new Dictionary<AudioClip, ProgressiveAudio>();
        readonly List<ProgressiveAudio> startingStreams = new List<ProgressiveAudio>();
        GameAudioInfo pendingAudio, cachePlaying;
        float cacheChangedAt;
        AudioClip bufferingClip;
        bool streamInternalControl, streamResume;
        int streamFrame;
        void SetupProgressiveAudio()
        {
            sessionPauseOverride = delegate(bool value) { return bufferingClip != null ? !streamResume : value; };
            harmony.Patch(AccessTools.Method(typeof(MusicService), "Pause"), prefix: new HarmonyMethod(typeof(Plugin), "StreamPause"));
            harmony.Patch(AccessTools.Method(typeof(MusicService), "UnPause"), prefix: new HarmonyMethod(typeof(Plugin), "StreamUnPause"));
        }
        static void StreamPause() { if (Instance != null && !Instance.streamInternalControl && Instance.bufferingClip != null) Instance.streamResume = false; }
        static void StreamUnPause() { if (Instance != null && !Instance.streamInternalControl && Instance.bufferingClip != null) Instance.streamResume = true; }
        SongUrl RefreshProgressiveUrl(long id)
        {
            lock (nativeGate)
            {
                SongUrl value = JsonConvert.DeserializeObject<SongUrl>(Bridge.SongURL(id));
                if (value == null || string.IsNullOrEmpty(value.url)) throw new PlaybackFailure("取地址", "RESPONSE", "重试音源未完成，请重新点播。", true);
                return value;
            }
        }
        IEnumerator NativeProgressiveClip(GameAudioInfo info, Song song, SongUrl url, Func<bool> canceled, UniTaskCompletionSource<AudioClip> completion)
        {
            ProgressiveAudio stream = ProgressiveAudio.Start(url, song.duration, delegate { return RefreshProgressiveUrl(song.id); });
            startingStreams.Add(stream);
            bool delivered = false;
            try
            {
                while (!stream.Ready.IsCompleted)
                {
                    if (canceled()) { completion.TrySetCanceled(); yield break; }
                    status = "正在缓冲「" + song.name + "」 " + (stream.Downloaded / 1048576d).ToString("F1") + " MB · 缓冲后开播";
                    yield return null;
                }
                if (canceled() || stream.Ready.IsCanceled) { completion.TrySetCanceled(); yield break; }
                if (stream.Ready.IsFaulted) { completion.TrySetException(stream.Ready.Exception.GetBaseException()); yield break; }
                AudioClip clip = AudioClip.Create(CloudPrefix + song.id, stream.Frames, stream.Channels, stream.SampleRate, true, stream.Read, stream.Seek);
                if (clip == null) { completion.TrySetException(new PlaybackFailure("创建音频", "AUDIO_CLIP", "游戏无法创建流式音频。", false)); yield break; }
                progressiveClips.Add(clip, stream); info.AudioClip = clip;
                nativeClips.Remove(info); nativeClips.Add(info);
                delivered = true;
                status = "边下边播 · " + song.name;
                completion.TrySetResult(clip);
            }
            finally
            {
                startingStreams.Remove(stream);
                if (!delivered) { stream.Dispose(); if (pendingAudio == info) pendingAudio = null; }
            }
        }
        void CloseProgressiveAudio()
        {
            foreach (var stream in startingStreams) stream.Dispose();
            startingStreams.Clear();
            foreach (var stream in progressiveClips.Values) stream.Dispose();
            progressiveClips.Clear(); bufferingClip = null;
        }
        void ProgressiveTickSafe()
        {
            if (destroying) return;
            try { ProgressiveTick(); }
            catch (Exception) { NativeNotice("音频缓冲状态读取未完成，请重新点播。", 10); }
        }
        void ProgressiveTick()
        {
            var service = NativeService;
            if (service == null) return;
            var info = service.PlayingMusic;
            if (info != cachePlaying) { cachePlaying = info; cacheChangedAt = Time.realtimeSinceStartup; }
            if (pendingAudio == info) pendingAudio = null;
            if (Time.realtimeSinceStartup - cacheChangedAt > 1)
                for (int i = nativeClips.Count - 1; i >= 0; i--)
                    if (nativeClips[i] != info && nativeClips[i] != pendingAudio)
                    { ReleaseNativeClip(nativeClips[i]); nativeClips.RemoveAt(i); }
            ProgressiveAudio stream;
            if (info == null || info.AudioClip == null || !progressiveClips.TryGetValue(info.AudioClip, out stream)) { bufferingClip = null; return; }
            var source = PlaybackSessionSource(info);
            if (source == null) return;
            if (stream.Failure != null)
            {
                streamInternalControl = true;
                try { service.Pause(); } finally { streamInternalControl = false; }
                Logger.LogWarning(PlaybackFailure.Diagnostic(CloudSongId(info), stream.Failure));
                NativeNotice("播放已暂停：" + stream.Failure.Message + " 请重新点播。", 15);
                ReleaseNativeClip(info); nativeClips.Remove(info); bufferingClip = null;
                return;
            }
            if (stream.WaitingFrame >= 0 && bufferingClip != info.AudioClip)
            {
                bufferingClip = info.AudioClip; streamResume = !service.IsPause;
                streamFrame = Math.Max(0, Math.Min(stream.WaitingFrame, source.timeSamples));
            }
            if (bufferingClip == info.AudioClip)
            {
                streamInternalControl = true;
                try
                {
                    if (!service.IsPause || source.isPlaying) service.Pause();
                    // A new seek while waiting replaces the previous target.
                    if (source.timeSamples > streamFrame + stream.SampleRate || source.timeSamples < streamFrame - stream.SampleRate) streamFrame = source.timeSamples;
                    if (stream.CanResume(streamFrame))
                    {
                        source.timeSamples = streamFrame; stream.Seek(streamFrame);
                        bool resume = streamResume; bufferingClip = null;
                        if (resume) service.UnPause();
                    }
                    else status = "正在缓冲 · 已载入 " + (stream.AvailableFrames / (double)stream.SampleRate).ToString("F0") + " 秒，等待网络…";
                }
                finally { streamInternalControl = false; }
            }
            else if (!stream.Complete) status = "边下边播 · 已载入 " + (stream.AvailableFrames / (double)stream.SampleRate).ToString("F0") + " 秒 · " + (stream.Downloaded / 1048576d).ToString("F1") + " MB";
        }
    }
}
