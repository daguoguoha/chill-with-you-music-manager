// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.RegularExpressions;

namespace ChillNeteasePlaylist
{
    internal sealed class PlaybackFailure : InvalidOperationException
    {
        internal readonly string Stage, Code;
        internal readonly bool Retryable;
        internal PlaybackFailure(string stage, string code, string message, bool retryable)
            : base(stage + "失败：" + message)
        { Stage = stage; Code = code; Retryable = retryable; }

        internal static bool RetryDownload(bool connectionError, long http)
        { return connectionError || http == 408 || http == 429 || (http >= 500 && http <= 599) || http == 401 || http == 403 || http == 404 || http == 410; }

        internal static PlaybackFailure FromSource(string reason)
        {
            string value = (reason ?? "").ToLowerInvariant();
            Match network = Regex.Match(reason ?? "", @"\b(NETWORK_(?:PROXY|TLS|DNS|TIMEOUT|REFUSED|RESET|IO)|RESPONSE)$");
            if (network.Success)
            {
                string kind = network.Groups[1].Value;
                string message = kind == "NETWORK_PROXY" ? "代理连接未完成，请检查当前代理是否可用。"
                    : kind == "NETWORK_TLS" ? "安全连接未完成，请检查系统时间和网络证书。"
                    : kind == "NETWORK_DNS" ? "无法解析网易云地址，请检查网络和 DNS。"
                    : kind == "NETWORK_TIMEOUT" ? "连接网易云超时，请恢复网络后重试。"
                    : kind == "RESPONSE" ? "网易云返回的歌曲响应无效，请稍后重试。"
                    : "连接网易云未完成，请恢复网络后重试。";
                return new PlaybackFailure("取地址", kind, message, true);
            }
            Match code = Regex.Match(value, @"(?:code|status)\s*[:=]?\s*(\d{3})\b");
            string number = code.Success ? code.Groups[1].Value : "";
            if (number == "301" || number == "401" || number == "403")
                return new PlaybackFailure("取地址", "AUTH_" + number, "网易云拒绝访问，请检查登录状态和歌曲权限。", false);
            if (value.Contains("parse") || value.Contains("json"))
                return new PlaybackFailure("取地址", "RESPONSE", "网易云返回的歌曲响应无效，请稍后重试。", true);
            return new PlaybackFailure("取地址", number.Length == 0 ? "NETWORK" : "API_" + number,
                number.Length == 0 ? "连接网易云未完成，请稍后重试。" : "网易云接口返回状态 " + number + "，请稍后重试。", true);
        }

        internal static bool StopQueue(Exception error)
        {
            if (error is SongUnavailableException) return false;
            PlaybackFailure failure = error as PlaybackFailure;
            if (failure == null) return true;
            string code = failure.Code;
            if (failure.Stage == "取地址")
                return code != "STALE_QUEUE" && code != "INVALID_URL" && code != "UNSUPPORTED_FORMAT" && code != "API_404" && code != "API_410";
            if (failure.Stage == "下载")
                return code.StartsWith("NETWORK_", StringComparison.Ordinal) || code.StartsWith("ConnectionError_", StringComparison.Ordinal) || code.StartsWith("SEND_", StringComparison.Ordinal)
                    || Regex.IsMatch(code, @"^ProtocolError_HTTP_(?:401|403|408|429|5[0-9]{2})$");
            return Regex.IsMatch(code, @"^(?:UnauthorizedAccessException|DirectoryNotFoundException|FileNotFoundException|FileLoadException|DllNotFoundException|BadImageFormatException|OutOfMemoryException)_");
        }

        internal static string Diagnostic(long id, Exception error)
        {
            PlaybackFailure failure = error as PlaybackFailure;
            return "网易云播放失败 songId=" + id + " stage=" + (failure == null ? "加载" : failure.Stage)
                + " code=" + (failure == null ? error.GetType().Name : failure.Code)
                + " hresult=0x" + error.HResult.ToString("X8");
        }
        internal static PlaybackFailure Decode(Exception error, string format)
        {
            string message;
            if ((uint)error.HResult == 0x80070070) message = "系统临时目录空间不足，请清理空间后重试。";
            else if (error is System.IO.DirectoryNotFoundException) message = "系统临时目录不可用，请检查临时目录设置。";
            else if (error is UnauthorizedAccessException) message = "系统临时目录不可写，请检查文件权限。";
            else if (error is System.IO.FileNotFoundException || error is System.IO.FileLoadException || error is DllNotFoundException || error is BadImageFormatException)
                message = "音频解码依赖缺失或无法加载，请使用完整包执行安装 / 修复。";
            else if (error is OutOfMemoryException) message = "音频解码内存不足，请关闭其他应用后重试。";
            else if (format == "mp3") message = "MP3 解码未完成，音频可能损坏，请重新点播。";
            else message = "Windows 无法解码此格式，请检查系统媒体组件（0x" + error.HResult.ToString("X8") + "）。";
            return new PlaybackFailure("解码", error.GetType().Name + "_" + error.HResult.ToString("X8"), message, false);
        }
    }
}
