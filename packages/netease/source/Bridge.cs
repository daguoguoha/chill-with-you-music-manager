// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ChillNeteasePlaylist
{
    public static partial class Bridge
    {
        const string Dll = "ChillNetease.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseGetDailyRecommendSongs();
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int NeteaseInit(IntPtr directory);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int NeteaseIsLoggedIn();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int NeteaseRefreshLogin();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseGetUserInfo();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseQRGetKey();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseQRCheckStatus();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void NeteaseQRCancelLogin();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseGetUserPlaylists(int limit, int offset);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseGetPlaylistSongs(long id, int all);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr NeteaseGetSongURL(long id, IntPtr quality);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr NeteaseGetLastError();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void NeteaseFreeString(IntPtr text);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int NeteaseReportPlayback(long id, long playlistId, long seconds, int kind);

        static IntPtr Utf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
            IntPtr p = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            return p;
        }
        public static int Init(string directory)
        {
            IntPtr p = Utf8(directory);
            try { return NeteaseInit(p); } finally { Marshal.FreeHGlobal(p); }
        }
        public static string QrResponse(bool poll)
        {
            for (int attempt = 0; ; attempt++)
            {
                IntPtr response = poll ? NeteaseQRCheckStatus() : NeteaseQRGetKey();
                if (response != IntPtr.Zero) return Take(response);
                IntPtr error = NeteaseGetLastError();
                string reason = error == IntPtr.Zero ? "" : Take(error);
                if (attempt == 0 && QrRequest.CanRetry(reason)) { System.Threading.Thread.Sleep(500); continue; }
                throw new QrRequestException(QrRequest.Describe(poll ? "poll" : "key", reason),QrRequest.CanRetry(reason));
            }
        }
        public static string SongURL(long id)
        {
            try { return SongURLQuality(id, "exhigh"); }
            catch (SongUnavailableException) { return SongURLQuality(id, "standard"); }
            catch (PlaybackFailure error)
            {
                if (!error.Code.StartsWith("AUTH_", StringComparison.Ordinal)) throw;
                return SongURLQuality(id, "standard");
            }
        }
        static string SongURLQuality(long id, string quality)
        {
            IntPtr p = Utf8(quality);
            try
            {
                IntPtr response = NeteaseGetSongURL(id, p);
                if (response == IntPtr.Zero)
                {
                    IntPtr error = NeteaseGetLastError();
                    string reason = error == IntPtr.Zero ? null : Take(error);
                    if (SongRequestPolicy.IsUnavailable(reason)) throw new SongUnavailableException();
                    throw PlaybackFailure.FromSource(reason);
                }
                return Take(response);
            }
            finally { Marshal.FreeHGlobal(p); }
        }
        public static string Take(IntPtr p)
        {
            if (p == IntPtr.Zero) throw new InvalidOperationException("网易云请求失败，请检查网络、登录状态或歌曲权限。");
            try
            {
                int length = 0;
                while (length < 32 * 1024 * 1024 && Marshal.ReadByte(p, length) != 0) length++;
                if (length == 32 * 1024 * 1024) throw new InvalidOperationException("网易云响应过大。");
                byte[] bytes = new byte[length];
                Marshal.Copy(p, bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally { NeteaseFreeString(p); }
        }
    }
}
