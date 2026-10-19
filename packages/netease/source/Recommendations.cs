// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ChillNeteasePlaylist {
    public sealed partial class Plugin {
        const long RecommendationId = -2;
        const string RecommendationTitle = "每日推荐";
        void OpenRecommendations() {
            if (!logged || !ready || busy || destroying) return;
            status = "正在读取每日推荐…";
            Run(delegate {
                var response = JsonConvert.DeserializeObject<List<Song>>(Bridge.Take(Bridge.NeteaseGetDailyRecommendSongs()));
                return RecommendationPolicy.Prepare(response, song => Convert.ToString(song.id));
            }, delegate(object result) {
                songs = (List<Song>)result;
                selectedPlaylistId = RecommendationId; selectedPlaylistTitle = RecommendationTitle;
                query = ""; songScroll = Vector2.zero; showImport = false;
                var item = playlists.Find(p => p.id == RecommendationId);
                if (item != null) item.songCount = songs.Count;
                status = RecommendationTitle + " · " + songs.Count + " 首，点击歌曲播放。再次打开可刷新推荐。";
                RegisterNativePlaylist();
            });
        }
    }
}
