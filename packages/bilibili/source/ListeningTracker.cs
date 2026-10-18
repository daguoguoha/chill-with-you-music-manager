// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ChillBilibiliPlaylist
{
    // Counts continuous audio advancement bounded by monotonic elapsed time.
    // It never infers listening from the seek position or the track duration.
    public sealed class ListeningTracker
    {
        double lastClock, lastPosition, duration, elapsed;
        bool baseline, wasPlaying;
        public long SongId { get; private set; }
        public long PlaylistId { get; private set; }
        public int Seconds { get { return (int)Math.Floor(Math.Min(elapsed, duration)); } }
        public bool Started { get; set; }

        public void Begin(long songId, long playlistId, double length)
        {
            Clear();
            if (songId <= 0 || !Finite(length) || length <= 0) return;
            SongId = songId; PlaylistId = Math.Max(0, playlistId);
            duration = Math.Min(length, 86400);
        }
        public void Sample(double clock, double position, bool playing, double rate)
        {
            if (SongId == 0 || !Finite(clock) || !Finite(position) || position < 0 || !Finite(rate) || rate <= 0)
            { BreakContinuity(); return; }
            if (baseline && wasPlaying && playing)
            {
                double wall = clock - lastClock, advance = position - lastPosition;
                // Long scheduler/sleep gaps and discontinuities are deliberately uncounted.
                if (wall > 0 && wall <= 2 && advance > 0 && advance <= wall * rate + 0.15)
                    elapsed = Math.Min(duration, elapsed + Math.Min(wall, advance / rate));
            }
            lastClock = clock; lastPosition = position; wasPlaying = playing; baseline = true;
        }
        public void BreakContinuity() { baseline = false; wasPlaying = false; }
        public void Clear()
        {
            SongId = PlaylistId = 0; elapsed = duration = 0; Started = false; BreakContinuity();
        }
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
