// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json.Linq;
namespace ChillNeteasePlaylist
{
    public static partial class Bridge
    {
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr NeteaseGetLyrics(long id);
    }
    public sealed partial class Plugin
    {
        void SetupNativeLyrics()
        {
            LyricSource.NeteaseRequest = delegate(long id, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                JObject response = JObject.Parse(Bridge.Take(Bridge.NeteaseGetLyrics(id)));
                token.ThrowIfCancellationRequested();
                string error = (string)response["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(error, @"^(?:NETWORK_(?:PROXY|TLS|DNS|TIMEOUT|REFUSED|RESET|IO)|RESPONSE|HTTP_[0-9]{3})$")) error = "RESPONSE";
                    throw new PlaybackFailure("歌词", error, "网易云歌词连接未完成，请稍后重试。", true);
                }
                return response;
            };
        }
    }
}
