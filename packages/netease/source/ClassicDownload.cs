// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace ChillNeteasePlaylist
{
    public sealed partial class Plugin
    {
        IEnumerator ClassicDownload(Song song, SongUrl url, Func<bool> canceled, TaskCompletionSource<byte[]> result, Func<bool> quiet)
        {
            // Both classic transfer and streaming fallback use the same direct transport.
            using (var download = new DirectAudioTransfer(url.url, url.size))
            {
                float began = Time.realtimeSinceStartup, progressAt = began, nextStatus = began;
                long previousBytes = 0;
                while (!download.Task.IsCompleted)
                {
                    if (canceled()) { result.TrySetCanceled(); yield break; }
                    float now = Time.realtimeSinceStartup;
                    if (now - began >= ClassicPlaybackPolicy.StartupSeconds && ClassicPlaybackPolicy.CanStream((url.type ?? "").ToLowerInvariant(), url.isTrial, song.duration))
                    { result.TrySetException(new PlaybackFailure("下载", "BUFFER_FALLBACK", "切换为缓冲播放。", true)); yield break; }
                    long bytes = download.Bytes;
                    if (bytes > AudioDownload.Limit) { result.TrySetException(new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false)); yield break; }
                    if (bytes != previousBytes) { previousBytes = bytes; progressAt = now; }
                    if (now - progressAt >= ClassicPlaybackPolicy.IdleSeconds || now - began >= AudioDownload.DeadlineSeconds(url.size))
                    { result.TrySetException(new PlaybackFailure("下载", "NETWORK_TIMEOUT", "当前音频连接未完成。", true)); yield break; }
                    if (!quiet() && now >= nextStatus)
                    {
                        nextStatus = now + 0.25f;
                        status = "正在加载「" + song.name + "」 " + (bytes / 1048576d).ToString("F1") + (url.size > 0 ? " / " + (url.size / 1048576d).ToString("F1") : "") + " MB";
                    }
                    yield return null;
                }
                if (canceled()) { result.TrySetCanceled(); yield break; }
                if (download.Task.IsCanceled) { result.TrySetCanceled(); yield break; }
                if (download.Task.IsFaulted) { result.TrySetException(download.Task.Exception.GetBaseException()); yield break; }
                byte[] encoded = download.Task.Result;
                if (encoded == null || encoded.Length == 0) result.TrySetException(new PlaybackFailure("下载", "EMPTY_BODY", "服务器返回空音频。", true));
                else if (encoded.Length > AudioDownload.Limit) result.TrySetException(new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false));
                else result.TrySetResult(encoded);
            }
        }
    }
}
