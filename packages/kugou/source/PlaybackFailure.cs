// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.RegularExpressions;

namespace ChillKuGouPlaylist
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
            Match code = Regex.Match(value, @"(?:code|status)\s*[:=]?\s*(\d{3})\b");
            string number = code.Success ? code.Groups[1].Value : "";
            if (number == "301" || number == "401" || number == "403")
                return new PlaybackFailure("取地址", "AUTH_" + number, "酷狗拒绝访问，请检查登录状态和歌曲权限。", false);
            if (value.Contains("parse") || value.Contains("json"))
                return new PlaybackFailure("取地址", "RESPONSE", "酷狗返回的歌曲响应无效，请稍后重试。", true);
            return new PlaybackFailure("取地址", number.Length == 0 ? "NETWORK" : "API_" + number,
                number.Length == 0 ? "连接酷狗未完成，请稍后重试。" : "酷狗接口返回状态 " + number + "，请稍后重试。", true);
        }

        internal static string Diagnostic(string id, Exception error)
        {
            PlaybackFailure failure = error as PlaybackFailure;
            return "酷狗播放失败 stage=" + (failure == null ? "加载" : failure.Stage)
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
