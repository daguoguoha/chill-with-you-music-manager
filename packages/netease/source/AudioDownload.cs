// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace ChillNeteasePlaylist
{
    internal static class AudioDownload
    {
        internal const int Limit = 64 * 1024 * 1024;
        internal static int DeadlineSeconds(long expected)
        { return (int)Math.Max(120L, Math.Min(600L, 45L + Math.Max(0L, Math.Min(Limit, expected)) / 16384L)); }
        internal static Uri Target(string value)
        {
            if (!Rules.IsMusicUrl(value)) throw new PlaybackFailure("下载", "INVALID_URL", "音频地址不受支持。", false);
            Uri uri = new Uri(value);
            if (!uri.IsDefaultPort || uri.UserInfo.Length != 0) throw new PlaybackFailure("下载", "INVALID_URL", "音频地址不受支持。", false);
            return new Uri(Rules.SecureMusicUrl(value));
        }
        internal static byte[] ReadBounded(Stream input, long declared, CancellationToken token, int limit, Action<long> progress = null)
        {
            if (declared > limit) throw new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false);
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[65536];
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int count = input.Read(buffer, 0, Math.Min(buffer.Length, limit - (int)output.Length + 1));
                    if (count == 0) break;
                    if (output.Length + count > limit) throw new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false);
                    output.Write(buffer, 0, count);
                    if (progress != null) progress(output.Length);
                }
                token.ThrowIfCancellationRequested();
                if (output.Length == 0 || (declared >= 0 && declared != output.Length))
                    throw new PlaybackFailure("下载", "EMPTY_BODY", "音频下载不完整，请重试。", true);
                return output.ToArray();
            }
        }
        internal static PlaybackFailure Failure(WebException error)
        {
            var response = error.Response as HttpWebResponse;
            if (response != null)
            {
                long http = (long)response.StatusCode; response.Close();
                return new PlaybackFailure("下载", "ProtocolError_HTTP_" + http, "音频服务器返回 HTTP " + http + "。", PlaybackFailure.RetryDownload(false, http));
            }
            string kind = error.Status == WebExceptionStatus.ProxyNameResolutionFailure ? "PROXY"
                : error.Status == WebExceptionStatus.NameResolutionFailure ? "DNS"
                : error.Status == WebExceptionStatus.TrustFailure || error.Status == WebExceptionStatus.SecureChannelFailure ? "TLS"
                : error.Status == WebExceptionStatus.Timeout ? "TIMEOUT"
                : error.Status == WebExceptionStatus.ConnectFailure ? "REFUSED"
                : error.Status == WebExceptionStatus.ConnectionClosed || error.Status == WebExceptionStatus.KeepAliveFailure ? "RESET" : "IO";
            return new PlaybackFailure("下载", "NETWORK_" + kind, kind == "TLS" ? "音频安全连接未完成，请检查系统时间和网络证书。" : "音频网络连接未完成，请稍后重试。", true);
        }
        internal static byte[] Read(string value, CancellationToken canceled, long expected, Action<long> progress)
        { return Consume(value, canceled, expected, delegate(Stream input, long length, CancellationToken token) { return ReadBounded(input, length, token, Limit, progress); }); }
        internal static T Consume<T>(string value, CancellationToken canceled, long expected, Func<Stream, long, CancellationToken, T> consume)
        {
            Uri target = Target(value);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(canceled))
            {
                deadline.CancelAfter(DeadlineSeconds(expected) * 1000);
                for (int redirects = 0; ; redirects++)
                {
                    canceled.ThrowIfCancellationRequested();
                    var request = (HttpWebRequest)WebRequest.Create(target);
                    request.AllowAutoRedirect = false;
                    request.Timeout = 15000; request.ReadWriteTimeout = 20000;
                    request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36";
                    request.Referer = "https://music.163.com/";
                    // Use the runtime's normal system proxy and certificate validation.
                    // Never attach account cookies or disable TLS verification for CDN downloads.
                    using (deadline.Token.Register(delegate { request.Abort(); }))
                    {
                        try
                        {
                            using (var response = (HttpWebResponse)request.GetResponse())
                            {
                                int code = (int)response.StatusCode;
                                if (code >= 300 && code <= 399)
                                {
                                    if (redirects >= 3 || string.IsNullOrEmpty(response.Headers["Location"])) throw new PlaybackFailure("下载", "INVALID_URL", "音频重定向无效。", false);
                                    target = Target(new Uri(target, response.Headers["Location"]).AbsoluteUri); continue;
                                }
                                if (response.ContentLength > Limit) throw new PlaybackFailure("下载", "SIZE_LIMIT", "音频超过 64 MB。", false);
                                using (var input = response.GetResponseStream()) return consume(input, response.ContentLength, deadline.Token);
                            }
                        }
                        catch (WebException error)
                        {
                            if (canceled.IsCancellationRequested) throw new OperationCanceledException(canceled);
                            if (deadline.IsCancellationRequested) throw new PlaybackFailure("下载", "NETWORK_TIMEOUT", "音频下载超时，请恢复网络后重试。", true);
                            throw Failure(error);
                        }
                        catch (OperationCanceledException)
                        {
                            if (canceled.IsCancellationRequested) throw;
                            throw new PlaybackFailure("下载", "NETWORK_TIMEOUT", "音频下载超时，请恢复网络后重试。", true);
                        }
                    }
                }
            }
        }
    }
}
