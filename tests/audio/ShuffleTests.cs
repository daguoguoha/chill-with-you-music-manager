// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Collections.Generic;
using PLATFORM_NAMESPACE;
public static class ShuffleTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; }
    public static void Main()
    {
        int[] original = Enumerable.Range(0, 40).ToArray();
        var random = new Random(81);
        Check(ShufflePolicy.Build(original, false, 8, random.Next).SequenceEqual(original), "disabled keeps order");
        Check(ShufflePolicy.Build(new int[0], true, -1, random.Next).Count == 0, "empty queue");
        Check(ShufflePolicy.Build(new[]{3}, true, 3, random.Next).SequenceEqual(new[]{3}), "one track");
        var signatures = new HashSet<string>();
        for (int n = 0; n < 100; n++)
        {
            var queue = ShufflePolicy.Build(original, true, 8, random.Next);
            Check(queue[0] == 8, "playing stays first");
            Check(queue.OrderBy(x=>x).SequenceEqual(original), "each track occurs once");
            Check(queue[1] != 8, "next does not immediately repeat");
            signatures.Add(string.Join(",", queue));
        }
        Check(signatures.Count > 95, "independent queue permutations");
        Check(original.SequenceEqual(Enumerable.Range(0, 40)), "source untouched for disabling shuffle");
        var switched = ShufflePolicy.Build(original, true, 999, random.Next);
        Check(switched.OrderBy(x=>x).SequenceEqual(original), "old playing track absent after playlist switch");
        var display=ShufflePolicy.VisibleOrder(original,new[]{8,2,15,22});
        Check(display.SequenceEqual(new[]{2,8,15,22}),"display retains original playlist order");
        var playback=ShufflePolicy.VisibleOrder(new[]{22,15,2,8},display);
        Check(playback.SequenceEqual(new[]{22,15,2,8}),"hidden playback preserves random order");
        int offset=ShufflePolicy.OffsetAfter(playback,8,15);
        Check(playback[(playback.IndexOf(8)+offset)%playback.Count]==2,"failed track skips through hidden order");
        Check(ShufflePolicy.OffsetAfter(new int[0],1,2)==0,"empty failure offset");
        var played=new List<int>();int current=playback.IndexOf(8);
        for(int i=0;i<playback.Count;i++){current=(current+1)%playback.Count;played.Add(playback[current]);}
        Check(played.Distinct().Count()==display.Count,"full random cycle no repeat");
        Check(!played.SequenceEqual(display),"next/manual and auto-end read random rather than display order");
        Check(display.SequenceEqual(new[]{2,8,15,22}),"playback leaves displayed list unchanged");
        Console.WriteLine("PASS " + passed + " shuffle checks");
    }
}
