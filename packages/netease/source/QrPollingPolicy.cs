// SPDX-License-Identifier: GPL-3.0-or-later
namespace ChillNeteasePlaylist
{
    internal static class QrPollingPolicy
    {
        internal static bool Retry(bool transient,bool hasImage,int failures,float now,float deadline)
        { return transient && hasImage && failures>0 && failures<=3 && now<deadline; }
        internal static float Delay(int failures) { return failures<=1 ? 6 : failures==2 ? 12 : 24; }
    }
}
