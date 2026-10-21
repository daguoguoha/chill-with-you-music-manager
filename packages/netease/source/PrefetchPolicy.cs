// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ChillNeteasePlaylist
{
    internal static class PrefetchPolicy
    {
        // Mirrors the game's bounded forward search without loading or advancing it.
        internal static T Next<T>(IList<T> queue, T current, bool repeat, Func<T, bool> eligible) where T : class
        {
            if (queue == null || queue.Count == 0 || current == null) return null;
            if (repeat) return null; // The playing clip already serves repeat-one.
            int at = queue.IndexOf(current);
            for (int i = 0; i < queue.Count; i++)
            {
                T next = queue[(at + 1 + i) % queue.Count];
                if (next != null && eligible(next)) return next == current ? null : next;
            }
            return null;
        }
    }
}
