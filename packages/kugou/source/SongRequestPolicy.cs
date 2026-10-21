// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ChillKuGouPlaylist
{
    public sealed class SongUnavailableException : InvalidOperationException
    { public SongUnavailableException() : base("酷狗目前没有返回此曲的可用音源。") {} }
    internal static class SongRequestPolicy
    {
        internal static bool IsUnavailable(string reason)
        { return reason != null && reason.StartsWith("No URL available for this song", StringComparison.Ordinal); }
        internal static int NextOffset(int selected, int current, int count)
        { if (count <= 0) return 0; int offset = (selected-current+1)%count; return offset < 0 ? offset+count : offset; }
        internal static bool CanDeliver(int requested, int current, bool destroyed, bool canceled)
        { return requested == current && !destroyed && !canceled; }
        internal static T Resolve<T>(Func<T> request, Func<bool> canceled, Action delay)
        {
            for (int attempt=0;;attempt++)
            {
                if (canceled()) throw new OperationCanceledException();
                try { return request(); }
                catch (SongUnavailableException) { throw; }
                catch (PlaybackFailure error) { if (!error.Retryable || attempt >= 1) throw; delay(); }
                
            }
        }
    }
}
