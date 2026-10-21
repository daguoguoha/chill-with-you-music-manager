// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Threading;
using Bulbul;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace ChillNeteasePlaylist
{
    public sealed partial class Plugin
    {
        GameAudioInfo prefetchInfo, prefetchAttempt, prefetchPlaying, prefetchBuffered;
        CancellationTokenSource prefetchStop;
        UniTaskCompletionSource<AudioClip> prefetchCompletion;
        bool prefetchPromoted;
        int prefetchSelectionVersion;
        CancellationToken prefetchSelectionCancellation;
        bool PrefetchSelectionCanceled(GameAudioInfo info)
        {
            return prefetchInfo == info && prefetchPromoted &&
                (prefetchSelectionVersion != nativeLoadVersion || prefetchSelectionCancellation.IsCancellationRequested);
        }
        void CancelNativePrefetch()
        {
            var stop = prefetchStop;
            prefetchStop = null; prefetchInfo = null; prefetchCompletion = null; prefetchPromoted = false;
            if (stop != null) stop.Cancel();
        }
        GameAudioInfo NativePrefetchNext()
        {
            var service = NativeService;
            if (!OwnPlaylist(service) || !SaveDataManager.HasInstance) return null;
            var excluded = SaveDataManager.Instance.MusicSetting.ExcludedFromPlaylistUUIDs;
            return PrefetchPolicy.Next(NativeShuffleRead(service.CurrentPlayList), service.PlayingMusic,
                service.IsRepeatOneMusic, delegate(GameAudioInfo info) { return IsCloud(info) && !excluded.Contains(info.UUID); });
        }
        void NativePrefetchTick()
        {
            var service = NativeService;
            var current = service == null ? null : service.PlayingMusic;
            if (prefetchPlaying != current) { prefetchPlaying = current; prefetchAttempt = null; }
            if (pendingAudio != null) return; // User selection has priority, including a promoted preload.
            var next = NativePrefetchNext();
            if (prefetchBuffered != next) prefetchBuffered = null;
            if (prefetchInfo != null && prefetchInfo != next) CancelNativePrefetch();
            if (next != prefetchAttempt) prefetchAttempt = null;
            if (destroying || !IsCloud(current) || current.AudioClip == null || next == null || next.AudioClip != null
                || prefetchStop != null || next == prefetchAttempt || Time.realtimeSinceStartup - cacheChangedAt < 1) return;
            ProgressiveAudio stream;
            // Do not compete with the current song's active network stream.
            if (progressiveClips.TryGetValue(current.AudioClip, out stream) && !stream.Complete) return;
            prefetchAttempt = next; prefetchInfo = next; prefetchPromoted = false;
            var stop = new CancellationTokenSource(); prefetchStop = stop;
            var completion = new UniTaskCompletionSource<AudioClip>(); prefetchCompletion = completion;
            StartCoroutine(NativePrefetchRun(next, stop, completion));
        }
        IEnumerator NativePrefetchRun(GameAudioInfo info, CancellationTokenSource stop, UniTaskCompletionSource<AudioClip> completion)
        {
            float began = Time.realtimeSinceStartup;
            IEnumerator worker = NativeLoadClip(info, stop.Token, 0, completion, true);
            try
            {
                while (worker.MoveNext()) yield return worker.Current;
                try
                {
                    var clip = completion.Task.GetAwaiter().GetResult();
                    if (clip != null)
                    {
                        prefetchBuffered = info;
                        Logger.LogInfo("网易云预载完成 elapsedMs=" + ((Time.realtimeSinceStartup - began) * 1000).ToString("F0"));
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { Logger.LogWarning(PlaybackFailure.Diagnostic(CloudSongId(info), error) + " prefetch=true"); }
            }
            finally
            {
                var disposable = worker as IDisposable; if (disposable != null) disposable.Dispose();
                if (prefetchStop == stop) { prefetchStop = null; prefetchInfo = null; prefetchCompletion = null; prefetchPromoted = false; }
                stop.Dispose();
            }
        }
        void TrimNativeAudioCache(GameAudioInfo added)
        {
            var current = NativeService == null ? null : NativeService.PlayingMusic;
            int count = 0; long retained = 0;
            for (int i = nativeClips.Count - 1; i >= 0; i--)
            {
                var info = nativeClips[i]; var clip = info.AudioClip;
                bool active = info == current, pending = info == pendingAudio || info == added || info == prefetchInfo;
                ProgressiveAudio stream;
                stream = null; if (clip != null) progressiveClips.TryGetValue(clip, out stream);
                bool healthy = clip != null && (stream == null || stream.Failure == null);
                bool complete = stream == null || stream.Complete;
                if (healthy && info == prefetchBuffered) pending = true;
                long bytes = clip == null ? 0 : (long)clip.samples * clip.channels * 4;
                if (!ClassicPlaybackPolicy.KeepRecent(active, pending, healthy, complete, count, retained, bytes))
                { ReleaseNativeClip(info); nativeClips.RemoveAt(i); }
                else if (!active && !pending) { count++; retained += bytes; }
            }
        }
    }
}
