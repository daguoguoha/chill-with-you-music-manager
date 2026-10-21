// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ChillNeteasePlaylist
{
    public sealed partial class Plugin
    {
        IEnumerator ClassicDownload(Song song, SongUrl url, Func<bool> canceled, TaskCompletionSource<byte[]> result, Func<bool> quiet)
        {
            // Restore the original Unity transfer path, retaining managed audio decoding.
            using (var download = UnityWebRequest.Get(AudioDownload.Target(url.url).AbsoluteUri))
            {
                download.timeout = 0; download.redirectLimit = 0;
                UnityWebRequestAsyncOperation operation = null;
                try { operation = download.SendWebRequest(); }
                catch (Exception error) { result.TrySetException(new PlaybackFailure("下载", "SEND_" + error.HResult.ToString("X8"), "音频连接未开始。", true)); }
                if (operation == null) yield break;
                float began = Time.realtimeSinceStartup, progressAt = began, nextStatus = began;
                ulong previousBytes = 0;
                while (!operation.isDone)
                {
                    if (canceled()) { download.Abort(); result.TrySetCanceled(); yield break; }
                    float now = Time.realtimeSinceStartup;
                    if (now - began >= ClassicPlaybackPolicy.StartupSeconds && ClassicPlaybackPolicy.CanStream((url.type ?? "").ToLowerInvariant(), url.isTrial, song.duration))
                    { download.Abort(); result.TrySetException(new PlaybackFailure("下载", "BUFFER_FALLBACK", "切换为缓冲播放。", true)); yield break; }
                    ulong bytes = download.downloadedBytes;
                    if (bytes > (ulong)AudioDownload.Limit) { download.Abort(); result.TrySetException(new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false)); yield break; }
                    if (bytes != previousBytes) { previousBytes = bytes; progressAt = now; }
                    if (now - progressAt >= ClassicPlaybackPolicy.IdleSeconds || now - began >= AudioDownload.DeadlineSeconds(url.size))
                    { download.Abort(); result.TrySetException(new PlaybackFailure("下载", "NETWORK_TIMEOUT", "当前音频连接未完成。", true)); yield break; }
                    if (!quiet() && now >= nextStatus)
                    {
                        nextStatus = now + 0.25f;
                        status = "正在加载「" + song.name + "」 " + (bytes / 1048576d).ToString("F1") + (url.size > 0 ? " / " + (url.size / 1048576d).ToString("F1") : "") + " MB";
                    }
                    yield return null;
                }
                if (canceled()) { result.TrySetCanceled(); yield break; }
                if (download.result != UnityWebRequest.Result.Success || (download.responseCode >= 300 && download.responseCode <= 399))
                {
                    bool connection = download.result == UnityWebRequest.Result.ConnectionError;
                    result.TrySetException(new PlaybackFailure("下载", (download.responseCode >= 300 && download.responseCode <= 399 ? "ProtocolError" : download.result.ToString()) + "_HTTP_" + download.responseCode,
                        "音频连接未完成。", PlaybackFailure.RetryDownload(connection, download.responseCode) || (download.responseCode >= 300 && download.responseCode <= 399)));
                    yield break;
                }
                byte[] encoded = download.downloadHandler.data;
                if (encoded == null || encoded.Length == 0) result.TrySetException(new PlaybackFailure("下载", "EMPTY_BODY", "服务器返回空音频。", true));
                else if (encoded.Length > AudioDownload.Limit) result.TrySetException(new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false));
                else result.TrySetResult(encoded);
            }
        }
    }
}
