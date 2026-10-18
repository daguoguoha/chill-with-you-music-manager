// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillSaltPlayerPlaylist
{
    public sealed partial class Plugin
    {
        Font font;
        GUISkin panelSkin;
        readonly List<Texture2D> uiTextures = new List<Texture2D>();
        GUIStyle titleText, headingText, bodyText, mutedText, smallText, accentText, statusText;
        GUIStyle windowStyle, sidebarStyle, cardStyle, rowStyle, selectedStyle;
        GUIStyle buttonStyle, quietButton, primaryButton, inputStyle, badgeText;
        string selectedPlaylistId;
        string selectedPlaylistTitle = "我的音乐", playlistQuery = "", cachedQuery;
        bool showImport;
        List<Song> cachedSongs;
        readonly List<int> filteredSongs = new List<int>();
        readonly Color ink = GameTheme.Ink;
        readonly Color muted = GameTheme.Muted;
        readonly Color accent = GameTheme.Accent;

        Texture2D Texture(Color color, bool rounded)
        {
            Texture2D texture=GameTheme.Surface(color,rounded);
            uiTextures.Add(texture);return texture;
        }
        GUIStyle emblemStyle;
        Texture2D dividerTexture;
        bool DrawLauncher(Rect bounds,string label)
        {
            bool clicked=GUI.Button(bounds,new GUIContent("",label),quietButton);
            GUI.Box(new Rect(bounds.x,bounds.y,42,42),"",emblemStyle);
            GUI.Label(new Rect(bounds.x+13,bounds.y+7,26,28),"♪",bodyText);
            GUI.Label(new Rect(bounds.x+50,bounds.y+7,bounds.width-50,28),label,mutedText);
            return clicked;
        }
        void ThemeChrome()
        {
            GUI.DrawTexture(new Rect(20,82,960,1),dividerTexture);
            GUI.DrawTexture(new Rect(276,91,1,472),dividerTexture);
            GUI.DrawTexture(new Rect(20,571,960,1),dividerTexture);
        }
        GUIStyle BoxStyle(Color color)
        {
            GUIStyle style = new GUIStyle();
            style.normal.background = Texture(color, true);
            style.border = new RectOffset(8, 8, 8, 8);
            return style;
        }
        GUIStyle TextStyle(int size, Color color, bool bold)
        {
            GUIStyle style = new GUIStyle();
            style.font = font; style.fontSize = size;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.normal.textColor = color; style.alignment = TextAnchor.MiddleLeft;
            style.clipping = TextClipping.Clip; return style;
        }
        GUIStyle ButtonStyle(Color background, Color hover, Color text)
        {
            GUIStyle style = new GUIStyle();
            style.font = font; style.fontSize = 15; style.alignment = TextAnchor.MiddleCenter;
            style.border = new RectOffset(8, 8, 8, 8);
            style.padding = new RectOffset(10, 10, 2, 2);
            Texture2D normal = Texture(background, true), over = Texture(hover, true);
            foreach (GUIStyleState state in new[] { style.normal, style.onNormal, style.focused, style.onFocused })
            { state.background = normal; state.textColor = text; }
            foreach (GUIStyleState state in new[] { style.hover, style.onHover, style.active, style.onActive })
            { state.background = over; state.textColor = text; }
            return style;
        }
        void EnsureUI()
        {
            if (panelSkin != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 18);
            panelSkin = Instantiate(GUI.skin); panelSkin.font = font;
            emblemStyle=new GUIStyle();emblemStyle.normal.background=GameTheme.Emblem();
            uiTextures.Add(emblemStyle.normal.background);
            dividerTexture=Texture(new Color(0.64f,0.59f,0.73f,0.24f),false);
            titleText = TextStyle(25, ink, false);
            headingText = TextStyle(21, ink, false);
            bodyText = TextStyle(16, ink, false);
            mutedText = TextStyle(14, muted, false);
            smallText = TextStyle(12, muted, false);
            statusText = TextStyle(13, muted, false); statusText.wordWrap = true; statusText.alignment = TextAnchor.UpperLeft;
            accentText = TextStyle(15, accent, false);
            badgeText = TextStyle(12, accent, false); badgeText.alignment = TextAnchor.MiddleRight;
            windowStyle = BoxStyle(GameTheme.Window);
            sidebarStyle = BoxStyle(GameTheme.Sidebar);
            cardStyle = BoxStyle(GameTheme.Card);
            selectedStyle = BoxStyle(GameTheme.Selected);
            rowStyle = ButtonStyle(new Color(0, 0, 0, 0), GameTheme.Hover, ink);
            buttonStyle = ButtonStyle(GameTheme.Button, GameTheme.Hover, ink);
            quietButton = ButtonStyle(new Color(0, 0, 0, 0), GameTheme.Hover, muted);
            primaryButton = ButtonStyle(GameTheme.Selected, GameTheme.Hover, ink);
            primaryButton.fontStyle = FontStyle.Normal;
            inputStyle = new GUIStyle(panelSkin.textField);
            inputStyle.font = font; inputStyle.fontSize = 15;
            inputStyle.alignment = TextAnchor.MiddleLeft;
            inputStyle.padding = new RectOffset(12, 12, 4, 4);
            inputStyle.border = new RectOffset(8, 8, 8, 8);
            Texture2D input = Texture(GameTheme.Input, true);
            Texture2D focused = Texture(GameTheme.Focused, true);
            inputStyle.normal.background = inputStyle.hover.background = input;
            inputStyle.focused.background = inputStyle.active.background = focused;
            inputStyle.normal.textColor = inputStyle.hover.textColor = inputStyle.focused.textColor = inputStyle.active.textColor = ink;
            panelSkin.verticalScrollbar = new GUIStyle();
            panelSkin.verticalScrollbar.fixedWidth = 8;
            panelSkin.verticalScrollbar.normal.background = Texture(new Color(0.12f,0.11f,0.19f,0.6f), true);
            panelSkin.verticalScrollbarThumb = BoxStyle(new Color(0.49f,0.45f,0.60f,0.8f));
            panelSkin.verticalScrollbarThumb.fixedWidth = 8;
            panelSkin.verticalScrollbarUpButton = new GUIStyle();
            panelSkin.verticalScrollbarDownButton = new GUIStyle();
            panelSkin.horizontalSlider = BoxStyle(new Color(0.38f,0.34f,0.48f,0.7f));
            panelSkin.horizontalSlider.fixedHeight = 4;
            panelSkin.horizontalSlider.margin = new RectOffset(0, 0, 8, 0);
            panelSkin.horizontalSliderThumb = BoxStyle(accent);
            panelSkin.horizontalSliderThumb.fixedWidth = 12;
            panelSkin.horizontalSliderThumb.fixedHeight = 12;
        }
        internal void RenderPanel()
        {
            if (firstGui) { firstGui = false; Logger.LogInfo("椒盐音乐运行对象 OnGUI 已开始。"); }
            EnsureUI();
            GUISkin previousSkin = GUI.skin;
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth; bool previousEnabled = GUI.enabled;
            float scale = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            try
            {
                GUI.skin = panelSkin; GUI.depth = -10000;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
                RenderLyrics();
                if (!visible)
                {
                    if (showLauncher.Value && DrawLauncher(new Rect(922, Screen.height / scale - 165, 170, 42), "椒盐音乐  ·  F6")) visible = true;
                    if (Time.realtimeSinceStartup < nativeNoticeUntil)
                    {
                        float noticeWidth = Mathf.Min(720, Screen.width / scale - 40);
                        Rect notice = new Rect((Screen.width / scale - noticeWidth) / 2, Screen.height / scale - 220, noticeWidth, 44);
                        GUI.Box(notice, "", cardStyle);
                        Label(new Rect(notice.x + 14, notice.y + 8, notice.width - 28, 28), Fit(status, bodyText, notice.width - 28), bodyText);
                    }
                    return;
                }
                panel.x = Mathf.Clamp(panel.x, 0, Mathf.Max(0, Screen.width / scale - panel.width));
                panel.y = Mathf.Clamp(panel.y, 0, Mathf.Max(0, Screen.height / scale - panel.height));
                if (!lyricsSettingsOpen) panel = GUI.Window(174943, panel, DrawPanel, "", windowStyle);
            }
            finally { LyricsGuiFocus(); GUI.skin = previousSkin; GUI.matrix = previousMatrix; GUI.depth = previousDepth; GUI.enabled = previousEnabled; }
        }
        string Input(Rect rect, string value, string name, string placeholder)
        {
            GUI.SetNextControlName(name);
            string result = GUI.TextField(rect, value, inputStyle);
            if (string.IsNullOrEmpty(result) && GUI.GetNameOfFocusedControl() != name)
                GUI.Label(new Rect(rect.x + 12, rect.y, rect.width - 24, rect.height), placeholder, mutedText);
            return result;
        }
        string Fit(string value, GUIStyle style, float width)
        {
            value = value ?? "";
            if (style.CalcSize(new GUIContent(value)).x <= width) return value;
            int low = 0, high = value.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (style.CalcSize(new GUIContent(value.Substring(0, middle) + "…")).x <= width) low = middle;
                else high = middle - 1;
            }
            if (low > 0 && char.IsHighSurrogate(value[low - 1])) low--;
            return value.Substring(0, low) + "…";
        }
        void Label(Rect rect, string value, GUIStyle style)
        { GUI.Label(rect, new GUIContent(Fit(value, style, rect.width), value), style); }
        void DrawPanel(int id)
        {
            ThemeChrome();
            GUI.Box(new Rect(24, 22, 42, 42), "", emblemStyle);
            GUI.Label(new Rect(33, 27, 30, 32), "盐", headingText);
            GUI.Label(new Rect(80, 18, 300, 36), "椒盐音乐", titleText);
            GUI.Label(new Rect(81, 52, 330, 22), "在放松时光里，听你的音乐", mutedText);
            GUI.enabled = ready && !busy;
            if (GUI.Button(new Rect(686, 27, 80, 36), "刷新", buttonStyle)) RefreshPlaylists();
            GUI.enabled = ready && !busy;
            if (GUI.Button(new Rect(776, 27, 110, 36), "导入音乐", buttonStyle)) showImport = true;
            GUI.enabled = true;
            if (GUI.Button(new Rect(896, 27, 80, 36), "收起 F6", quietButton)) visible = false;
            DrawSidebar();
            if (showImport || playlists.Count == 0) DrawImport();
            else DrawLibrary();
            DrawPlayer();
            GUI.DragWindow(new Rect(0, 0, 670, 82));
        }
        void DrawSidebar()
        {
            GUI.Box(new Rect(20, 91, 240, 424), "", sidebarStyle);
            GUI.Label(new Rect(36, 103, 155, 26), "我的歌单", accentText);
            GUI.Label(new Rect(195, 103, 46, 26), playlists.Count.ToString(), badgeText);
            playlistQuery = Input(new Rect(34, 140, 212, 34), playlistQuery, "playlist-search", "搜索歌单");
            List<Playlist> shown = playlists.FindAll(delegate(Playlist p) { return string.IsNullOrEmpty(playlistQuery) || p.name.IndexOf(playlistQuery, StringComparison.OrdinalIgnoreCase) >= 0; });
            Rect view = new Rect(30, 188, 220, 277);
            playlistScroll = GUI.BeginScrollView(view, playlistScroll, new Rect(0, 0, 207, Mathf.Max(view.height, shown.Count * 56)), false, false);
            GUI.enabled = ready && !busy;
            for (int i = 0; i < shown.Count; i++)
            {
                Playlist playlist = shown[i]; Rect row = new Rect(0, i * 56, 207, 52);
                bool selected = selectedPlaylistId == playlist.id;
                if (selected) GUI.Box(row, "", selectedStyle);
                if (GUI.Button(row, new GUIContent("", playlist.name), rowStyle)) OpenPlaylist(playlist.id, playlist.name);
                Label(new Rect(12, row.y + 5, 182, 24), playlist.name, selected ? accentText : bodyText);
                GUI.Label(new Rect(12, row.y + 29, 180, 18), playlist.songCount + " 首歌曲", smallText);
            }
            GUI.enabled = true; GUI.EndScrollView();
            GUI.enabled = ready && !busy;
            if (GUI.Button(new Rect(34, 477, 212, 28), showImport ? "返回歌曲列表" : "+  导入歌单 / 歌曲", quietButton)) showImport = !showImport;
            GUI.enabled = true;
            GUI.Label(new Rect(32, 525, 224, 23), "本地曲库", bodyText);
            GUI.Label(new Rect(32, 550, 224, 19), "音乐只在本机读取", smallText);
        }
        void UpdateFilter()
        {
            if (ReferenceEquals(cachedSongs, songs) && cachedQuery == query) return;
            cachedSongs = songs; cachedQuery = query; filteredSongs.Clear();
            for (int i = 0; i < songs.Count; i++)
                if (string.IsNullOrEmpty(query) || songs[i].Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) filteredSongs.Add(i);
        }
        Song CurrentSong()
        { return current >= 0 && current < queue.Length ? queue[current] : null; }
        static string Artists(Song song)
        { return song == null || song.artists == null ? "" : string.Join(" / ", song.artists.ToArray()); }
        static string TimeText(double seconds)
        { int total = Math.Max(0, (int)seconds); return (total / 60) + ":" + (total % 60).ToString("00"); }
        void DrawLibrary()
        {
            Label(new Rect(284, 92, 680, 38), selectedPlaylistTitle, headingText);
            GUI.Label(new Rect(285, 131, 510, 23), string.IsNullOrEmpty(selectedPlaylistId) ? "选择左侧歌单，开始收听" : songs.Count + " 首歌曲  ·  点击即可播放", mutedText);
            string search = Input(new Rect(284, 169, 690, 36), query, "song-search", "搜索歌曲或歌手");
            if (search != query) { query = search; songScroll = Vector2.zero; }
            UpdateFilter();
            GUI.Label(new Rect(300, 213, 40, 24), "#", smallText);
            GUI.Label(new Rect(343, 213, 340, 24), "歌曲", smallText);
            GUI.Label(new Rect(683, 213, 190, 24), "歌手", smallText);
            GUI.Label(new Rect(908, 213, 58, 24), "时长", smallText);
            if (filteredSongs.Count == 0)
            {
                GUI.Box(new Rect(502, 303, 92, 78), "", cardStyle);
                GUI.Label(new Rect(525, 316, 78, 45), "音乐", headingText);
                string message = songs.Count == 0 ? (string.IsNullOrEmpty(selectedPlaylistId) ? "选一个歌单，听一会儿" : "这个歌单暂时没有歌曲") : "没有找到匹配的歌曲";
                GUI.Label(new Rect(406, 400, 540, 30), message, headingText);
                GUI.Label(new Rect(407, 437, 545, 26), songs.Count == 0 ? "你的椒盐音乐收藏，就在左侧。" : "试试其他歌名，或搜索歌手。", mutedText);
                if (songs.Count > 0 && GUI.Button(new Rect(503, 481, 108, 32), "清空搜索", buttonStyle)) query = "";
                return;
            }
            Rect view = new Rect(284, 242, 690, 308);
            songScroll = GUI.BeginScrollView(view, songScroll, new Rect(0, 0, 675, Mathf.Max(view.height, filteredSongs.Count * 50)), false, false);
            int first = Mathf.Max(0, (int)(songScroll.y / 50) - 1);
            int last = Mathf.Min(filteredSongs.Count, (int)((songScroll.y + view.height) / 50) + 2);
            Song now = CurrentSong();
            GUI.enabled = ready && !busy;
            for (int rowIndex = first; rowIndex < last; rowIndex++)
            {
                int songIndex = filteredSongs[rowIndex]; Song song = songs[songIndex];
                Rect row = new Rect(0, rowIndex * 50, 675, 46);
                bool active = now != null && now.id == song.id && (playing || paused || request != null || busy);
                if (active) GUI.Box(row, "", selectedStyle);
                if (GUI.Button(row, new GUIContent("", song.Label), rowStyle)) Play(songs.ToArray(), songIndex, false);
                GUI.Label(new Rect(16, row.y + 9, 38, 28), active ? (paused ? "Ⅱ" : "♪") : (songIndex + 1).ToString("00"), active ? accentText : smallText);
                Label(new Rect(59, row.y + 8, 322, 30), song.name, active ? accentText : bodyText);
                Label(new Rect(399, row.y + 9, 220, 28), Artists(song), mutedText);
                GUI.Label(new Rect(628, row.y + 9, 44, 28), song.duration > 0 ? TimeText(song.duration) : "—", smallText);
            }
            GUI.enabled = true; GUI.EndScrollView();
            GUI.Label(new Rect(287, 553, 660, 23), string.IsNullOrEmpty(query) ? "列表循环播放" : "搜索结果  " + filteredSongs.Count + " / " + songs.Count, smallText);
        }
        void TogglePlayback()
        {
            if (audio != null && audio.clip != null)
            { if (paused) audio.UnPause(); else audio.Pause(); paused = !paused; return; }
            if (busy || !ready) return;
            if (queue.Length > 0 && current >= 0) Play(queue, current, false);
            else if (filteredSongs.Count > 0) Play(songs.ToArray(), filteredSongs[0], false);
        }
        void DrawPlayer()
        {
            GUI.Box(new Rect(20, 583, 960, 87), "", cardStyle);
            GUI.Label(new Rect(40, 598, 680, 29), "使用游戏原生播放器播放椒盐音乐歌曲", bodyText);
            if (GUI.Button(new Rect(40, 631, 230, 30), showLauncher.Value ? "隐藏游戏内入口" : "显示游戏内入口", quietButton))
            {
                showLauncher.Value = !showLauncher.Value;
                Config.Save();
            }
            if (GUI.Button(new Rect(286, 631, 435, 30), "歌词与同步设置", quietButton)) lyricsSettingsOpen = true;
            if (GUI.Button(new Rect(750, 604, 210, 40), "收起并使用原生播放器", primaryButton)) visible = false;
            GUI.Label(new Rect(30,681,938,36),new GUIContent(status,status),statusText);
        }
        void DisposeUI()
        {
            DisposeLyricsUI();
            if (panelSkin != null) Destroy(panelSkin);
            if (font != null) Destroy(font);
            foreach (Texture2D texture in uiTextures) if (texture != null) Destroy(texture);
            uiTextures.Clear();
        }
    }
}

