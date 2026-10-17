// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
namespace ChillQQMusicPlaylist
{
    public static class Bridge
    {
        const string Dll = "ChillQQMusic.dll";
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int QQMusicInit(IntPtr directory);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int QQMusicIsLoggedIn();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int QQMusicRefreshLogin();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr QQMusicGetUserInfo();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr QQMusicQRGetImage(IntPtr loginType);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr QQMusicQRCheckStatus();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void QQMusicQRCancelLogin();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr QQMusicGetUserPlaylists();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr QQMusicGetPlaylistSongs(long id);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr QQMusicGetLikeSongs(int all);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr QQMusicGetSongURL(IntPtr id, IntPtr quality);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void QQMusicFreeString(IntPtr text);
        static IntPtr Utf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
            IntPtr p = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, p, bytes.Length); return p;
        }
        public static int Init(string directory)
        {
            System.IO.Directory.CreateDirectory(directory); IntPtr p = Utf8(directory);
            try { return QQMusicInit(p); } finally { Marshal.FreeHGlobal(p); }
        }
        public static string QrImage(string loginType)
        {
            if (loginType != "qq" && loginType != "wx") throw new ArgumentException("登录方式无效。");
            IntPtr p = Utf8(loginType);
            try { return Take(QQMusicQRGetImage(p)); } finally { Marshal.FreeHGlobal(p); }
        }
        public static List<Song> Playlist(long id)
        {
            if (id == -1) return JsonConvert.DeserializeObject<List<Song>>(Take(QQMusicGetLikeSongs(1))) ?? new List<Song>();
            PlaylistDetail detail = JsonConvert.DeserializeObject<PlaylistDetail>(Take(QQMusicGetPlaylistSongs(id)));
            if (detail == null) throw new InvalidOperationException("歌单响应为空。");
            if (detail.songCount > 0 && (detail.songs == null || detail.songs.Count < detail.songCount))
                throw new InvalidOperationException("上游仅返回部分歌曲，请拆分歌单后重试。");
            return detail.songs ?? new List<Song>();
        }
        public static string SongURL(string id)
        {
            IntPtr mid = Utf8(id), quality = Utf8("128");
            try { return Take(QQMusicGetSongURL(mid, quality)); }
            finally { Marshal.FreeHGlobal(mid); Marshal.FreeHGlobal(quality); }
        }
        public static string Take(IntPtr p)
        {
            if (p == IntPtr.Zero) throw new InvalidOperationException("QQ 音乐请求失败，请检查网络、登录状态或歌曲权限。");
            try
            {
                int length = 0;
                while (length < 32 * 1024 * 1024 && Marshal.ReadByte(p, length) != 0) length++;
                if (length == 32 * 1024 * 1024) throw new InvalidOperationException("QQ 音乐响应过大。");
                byte[] bytes = new byte[length]; Marshal.Copy(p, bytes, 0, length); return Encoding.UTF8.GetString(bytes);
            }
            finally { QQMusicFreeString(p); }
        }
    }
}
