// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ChillBilibiliPlaylist
{
    internal static class ShufflePolicy
    {
        internal static List<T> VisibleOrder<T>(IEnumerable<T> order,IEnumerable<T> visible)
        { var members=new HashSet<T>(visible);return order.Where(members.Contains).ToList(); }
        internal static int OffsetAfter<T>(IList<T> queue,T playing,T failed)
        { if(queue.Count==0)return 0;int offset=(queue.IndexOf(failed)-queue.IndexOf(playing)+1)%queue.Count;return offset<0?offset+queue.Count:offset; }
        internal static List<T> Build<T>(IEnumerable<T> source, bool enabled, T playing, Func<int, int> next)
        {
            var list = new List<T>(source);
            if (!enabled) return list;
            for (int i = list.Count - 1; i > 0; i--)
            { int j = next(i + 1); T item = list[i]; list[i] = list[j]; list[j] = item; }
            int current = list.IndexOf(playing);
            if (current > 0) { list.RemoveAt(current); list.Insert(0, playing); }
            return list;
        }
    }
}
