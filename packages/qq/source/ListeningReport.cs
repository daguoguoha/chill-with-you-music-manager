// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
namespace ChillQQMusicPlaylist
{
    public static partial class Bridge
    {
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int QQMusicReportPlayback(long id,long seconds,long startedAt);
        public static int ReportPlayback(Song song,string playlist,long seconds,long position,long startedAt,long endedAt,int kind)
        {
            if(QQMusicIsLoggedIn()!=1)return 401;
            if(kind==0)return 202;
            if(song==null || song.numericId<=0 || seconds<1 || seconds>86400 || kind<1 || kind>3)return 400;
            return QQMusicReportPlayback(song.numericId,seconds,startedAt/1000);
        }
    }
}
