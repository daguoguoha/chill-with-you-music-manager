// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;
namespace ChillPlaybackSpeed
{
    internal sealed class SpeedController
    {
        AudioSource active;
        string track;
        float original, applied;
        internal static float Rate(float value)
        { return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Math.Max(0.5f, Math.Min(2, (float)Math.Round(value * 20) / 20)); }
        internal static bool Supported(string id)
        {
            if (id == null) return false;
            foreach (string prefix in new[] { "netease:", "qqmusic:", "qq:", "kugou:", "bilibili:", "ximalaya:", "saltplayer:" })
                if (id.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }
        internal void Apply(AudioSource source, string key, float rate, bool enabled)
        {
            if (!enabled || source == null || !Supported(key)) { Release(); return; }
            if (source != active || track != key)
            {
                Release(); active = source; track = key; original = source.pitch; applied = original;
            }
            applied = Rate(rate);
            if (Math.Abs(active.pitch - applied) > 0.0001f) active.pitch = applied;
        }
        internal void Release()
        {
            // Do not undo a pitch change made later by another owner or a newly reused player.
            if (active != null && Math.Abs(active.pitch - applied) < 0.0001f) active.pitch = original;
            active = null; track = null;
        }
    }
}
