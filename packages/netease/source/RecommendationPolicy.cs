// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ChillNeteasePlaylist {
    internal static class RecommendationPolicy {
        public static List<T> Prepare<T>(IEnumerable<T> source, Func<T,string> identity) where T:class {
            if (source == null) throw new InvalidOperationException("推荐响应为空，请稍后重试。");
            var result = new List<T>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            int count=0;
            foreach (var item in source) {
                if (++count > 2000) throw new InvalidOperationException("推荐响应过大，请稍后重试。");
                if (item == null) continue;
                string id = identity(item);
                if (!string.IsNullOrEmpty(id) && id != "0" && seen.Add(id)) result.Add(item);
            }
            if (result.Count == 0) throw new InvalidOperationException("暂未获取到推荐歌曲，请确认登录后稍后重试。");
            return result;
        }
    }
}
