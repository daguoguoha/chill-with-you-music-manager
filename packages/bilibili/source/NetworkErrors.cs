// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Net;

namespace ChillBilibiliPlaylist
{
    internal static class NetworkErrors
    {
        internal static void EnableModernTls()
        {
            // Unity's HttpWebRequest has no per-request SslProtocols property.
            // Leave OS defaults (0) alone; on older explicit policies add TLS 1.2
            // while retaining the protocol choices used by the game/other mods.
            SecurityProtocolType current = ServicePointManager.SecurityProtocol;
            if ((int)current != 0 && (current & SecurityProtocolType.Tls12) == 0)
                ServicePointManager.SecurityProtocol = current | SecurityProtocolType.Tls12;
        }

        internal static bool CanRetry(WebException error)
        {
            switch (error.Status)
            {
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.ConnectionClosed:
                case WebExceptionStatus.KeepAliveFailure:
                case WebExceptionStatus.ReceiveFailure:
                case WebExceptionStatus.SendFailure:
                case WebExceptionStatus.NameResolutionFailure:
                    return true;
                case WebExceptionStatus.ProtocolError:
                    HttpWebResponse response = error.Response as HttpWebResponse;
                    return response != null && ((int)response.StatusCode == 502 || (int)response.StatusCode == 503 || (int)response.StatusCode == 504);
                default:
                    return false;
            }
        }

        internal static string Describe(WebException error, string host)
        {
            string reason;
            switch (error.Status)
            {
                case WebExceptionStatus.Timeout: reason = "连接超时，请重试或换网络"; break;
                case WebExceptionStatus.NameResolutionFailure: reason = "域名解析失败，请检查 DNS 或换网络"; break;
                case WebExceptionStatus.ProxyNameResolutionFailure: reason = "代理地址解析失败，请检查系统代理"; break;
                case WebExceptionStatus.TrustFailure: reason = "证书验证失败，请检查系统时间和网络证书"; break;
                case WebExceptionStatus.SecureChannelFailure: reason = "TLS 安全连接失败，请检查系统更新或换网络"; break;
                case WebExceptionStatus.ConnectFailure: reason = "连接被拒绝或无法建立，请检查防火墙、代理或换网络"; break;
                case WebExceptionStatus.ProtocolError:
                    HttpWebResponse response = error.Response as HttpWebResponse;
                    reason = response == null ? "接口 HTTP 错误" : "接口 HTTP " + (int)response.StatusCode;
                    if (response != null && (int)response.StatusCode == 407) reason += "，请检查代理登录";
                    else reason += "，请稍后重试或换网络";
                    break;
                default: reason = "网络连接中断，请重试或换网络"; break;
            }
            // Only a fixed endpoint host and status enum; no URL, response body, cookies or account.
            return reason + " [" + error.Status + "; " + host + "]";
        }
    }
}
