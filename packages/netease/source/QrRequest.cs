// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.RegularExpressions;

namespace ChillNeteasePlaylist
{
    public sealed class QrRequestException : InvalidOperationException
    {
        public readonly bool Retryable;
        public QrRequestException(string message,bool retryable=false) : base(message) { Retryable=retryable; }
    }

    public static class QrRequest
    {
        // Native errors can contain URLs or login material. Only fixed messages and
        // validated numeric status codes may cross the UI/logging boundary.
        public static string Describe(string stage, string reason)
        {
            string prefix = stage == "poll" ? "检查扫码状态失败：" : "获取登录二维码失败：";
            string value = reason ?? "";
            Match code = Regex.Match(value, @"\b(?:code|status)\s*[:=]\s*(-?\d{1,5})\b", RegexOptions.IgnoreCase);
            if (code.Success) return prefix + "网易云返回状态码 " + code.Groups[1].Value + "，请稍后重试。";
            if (Has(value,"account file")) return prefix + "登录文件无法保存或读取，请检查插件账号目录的写入权限和剩余空间。";
            if (Has(value, "timeout") || Has(value, "timed out") || Has(value, "deadline exceeded"))
                return prefix + "连接超时，请检查网络后重新生成。";
            if (Has(value, "certificate") || Has(value, "x509") || Has(value, "tls"))
                return prefix + "安全连接验证失败，请检查系统时间和网络。";
            if (Has(value, "proxy")) return prefix + "代理连接失败，请检查现有代理是否可用。";
            if (Has(value, "no such host") || Has(value, "lookup")) return prefix + "域名解析失败，请检查网络。";
            if (Has(value, "connection") || Has(value, "EOF") || Has(value, "send request"))
                return prefix + "连接被关闭或未能连通，请检查网络后重试。";
            if (Has(value, "QR key not initialized")) return prefix + "二维码已失效，请重新生成。";
            if (Has(value, "parse") || Has(value, "JSON")) return prefix + "登录接口响应无效，请稍后重试。";
            return prefix + "请求未完成，请稍后重试。";
        }

        public static string LoginUrl(string value)
        {
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || (uri.Scheme!="http" && uri.Scheme!="https") || uri.Host!="music.163.com" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))return null;
            return new UriBuilder(uri){Scheme="https",Port=-1}.Uri.AbsoluteUri;
        }

        public static bool CanRetry(string reason)
        {
            string value = reason ?? "";
            if (Regex.IsMatch(value, @"\b(?:code|status)\s*[:=]\s*(-?\d{1,5})\b", RegexOptions.IgnoreCase)) return false;
            if(Has(value,"certificate") || Has(value,"x509") || Has(value,"tls") || (Has(value,"proxy") && Has(value,"407")))return false;
            return Has(value, "timed out") || Has(value, "timeout") || Has(value, "deadline exceeded") || Has(value, "proxy")
                || Has(value, "connection") || Has(value, "EOF") || Has(value, "lookup");
        }
        static bool Has(string source, string value)
        { return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0; }
    }
}
