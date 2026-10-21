// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ChillNeteasePlaylist
{
    internal static class ClassicPlaybackPolicy
    {
        internal const long RecentBytes = 256L * 1024 * 1024;
        internal const int RecentCount = 3;
        internal const float IdleSeconds = 10;
        internal const float StartupSeconds = 3;
        internal static bool CanStream(string format, bool trial, double seconds)
        { return format == "mp3" && !trial && seconds > 0 && seconds <= 5400; }
        internal static bool Compatibility(PlaybackFailure failure)
        {
            if (failure == null) return false;
            return failure.Code == "BUFFER_FALLBACK" || failure.Code.StartsWith("NETWORK_", StringComparison.Ordinal)
                || failure.Code.StartsWith("ConnectionError_", StringComparison.Ordinal)
                || failure.Code.StartsWith("SEND_", StringComparison.Ordinal)
                || System.Text.RegularExpressions.Regex.IsMatch(failure.Code, @"^ProtocolError_HTTP_3[0-9]{2}$");
        }
        internal static bool KeepRecent(bool active, bool pending, bool healthy, bool complete, int count, long retained, long bytes)
        {
            if (active || pending) return true;
            return healthy && complete && count < RecentCount && bytes > 0 && bytes <= RecentBytes && retained <= RecentBytes - bytes;
        }
    }
}
