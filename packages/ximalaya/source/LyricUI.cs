// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ChillXimalayaPlaylist
{
    public sealed partial class Plugin
    {
        GUIStyle lyricMain, lyricNext, lyricCaption, lyricShadow, lyricNextShadow, lyricWindowStyle;
        Rect lyricsSettingsRect = new Rect(410, 90, 780, 660);
        Vector2 lyricListScroll;
        Rect lyricOverlayRect;
        bool lyricDragPending;
        string lyricColorInput;
        float lyricMainHeight = 50, lyricNextHeight = 21;

        void EnsureLyricsUI()
        {
            if (lyricMain != null) return;
            lyricWindowStyle = new GUIStyle();
            lyricColorInput = lyricsColor.Value ?? "#F0EBFA";
            lyricMain = TextStyle(22, GameTheme.Ink, false);
            lyricMain.richText = false; lyricMain.wordWrap = true; lyricMain.alignment = TextAnchor.MiddleCenter;
            lyricNext = TextStyle(13, GameTheme.Muted, false); lyricNext.richText = false; lyricNext.wordWrap = true; lyricNext.alignment = TextAnchor.MiddleCenter;
            lyricShadow = new GUIStyle(lyricMain); lyricShadow.normal.textColor = new Color(0.025f, 0.025f, 0.05f, 0.86f);
            lyricNextShadow = new GUIStyle(lyricNext); lyricNextShadow.normal.textColor = new Color(0.025f, 0.025f, 0.05f, 0.82f);
            lyricCaption = TextStyle(10, GameTheme.Muted, false); lyricCaption.richText = false;
        }
        void RenderLyrics()
        {
            if (lyricsEnabled == null) return;
            EnsureLyricsUI();
            float scale = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
            var service = NativeService;
            var now = service == null ? null : service.PlayingMusic;
            if (lyricsEnabled.Value && lyricTrack != null && IsCloud(now) && now.UUID == lyricTrack.Key)
            {
                float width = Mathf.Min(720, screenWidth - 36);
                string currentText, nextText; LyricOverlayText(out currentText, out nextText);
                lyricMain.fontSize = lyricDocument.Lines.Count == 0 ? 16 : lyricsSize.Value;
                lyricMainHeight = Mathf.Max(50, lyricMain.CalcHeight(new GUIContent(currentText), width - 62) + 4);
                lyricNextHeight = Mathf.Max(21, lyricNext.CalcHeight(new GUIContent(nextText), width - 80) + 4);
                float height = 10 + lyricMainHeight + lyricNextHeight;
                if (!lyricDragPending)
                    lyricOverlayRect = new Rect(lyricsX.Value < 0 ? (screenWidth - width) * 0.5f : 12 + Mathf.Max(0, screenWidth - width - 24) * lyricsX.Value,
                        lyricsY.Value < 0 ? Mathf.Max(12, screenHeight - lyricsBottom.Value - height) : 12 + Mathf.Max(0, screenHeight - height - 24) * lyricsY.Value, width, height);
                lyricOverlayRect.height = height;
                Rect previous = lyricOverlayRect;
                lyricOverlayRect = GUI.Window(185004 + 20, lyricOverlayRect, DrawLyricOverlay, "", lyricWindowStyle);
                lyricOverlayRect.x = Mathf.Clamp(lyricOverlayRect.x, 12, Mathf.Max(12, screenWidth - width - 12));
                lyricOverlayRect.y = Mathf.Clamp(lyricOverlayRect.y, 12, Mathf.Max(12, screenHeight - height - 12));
                if (previous.x != lyricOverlayRect.x || previous.y != lyricOverlayRect.y) lyricDragPending = true;
                if (lyricDragPending && !RuntimeHost.IsLeftMouseDownInGame()) FlushLyricDrag();
            }
            if (lyricsSettingsOpen)
            {
                lyricsSettingsRect.width = Mathf.Min(780, screenWidth - 24);
                lyricsSettingsRect.height = Mathf.Min(660, screenHeight - 24);
                lyricsSettingsRect.x = Mathf.Clamp(lyricsSettingsRect.x, 0, Mathf.Max(0, screenWidth - lyricsSettingsRect.width));
                lyricsSettingsRect.y = Mathf.Clamp(lyricsSettingsRect.y, 0, Mathf.Max(0, screenHeight - lyricsSettingsRect.height));
                lyricsSettingsRect = GUI.Window(185004, lyricsSettingsRect, DrawLyricsSettings, "", windowStyle);
            }
        }
        void LyricOverlayText(out string currentText, out string nextText)
        {
            int index = lyricDocument.IndexAt(lyricPosition + lyricsOffset.Value);
            currentText = index >= 0 ? lyricDocument.Lines[index].Text : lyricDocument.Lines.Count > 0 ? "♪" : lyricsMessage;
            nextText = index + 1 >= 0 && index + 1 < lyricDocument.Lines.Count ? lyricDocument.Lines[index + 1].Text.Replace('\n', ' ') : "";
            if (!lyricDocument.Timed && lyricDocument.Lines.Count > 0) nextText = "纯文本歌词 · 无时间轴";
        }
        void DrawLyricOverlay(int id)
        {
                Rect bounds = new Rect(0, 0, lyricOverlayRect.width, lyricOverlayRect.height);
                float width = bounds.width;
                // The game scene remains completely visible behind the lyric glyphs.
                string currentText, nextText; LyricOverlayText(out currentText, out nextText);
                lyricMain.fontSize = lyricDocument.Lines.Count == 0 ? 16 : lyricsSize.Value;
                float red, green, blue;
                if (LyricPalette.TryRgb(lyricsColor.Value, out red, out green, out blue))
                { lyricMain.normal.textColor = new Color(red, green, blue, 1); lyricNext.normal.textColor = new Color(red, green, blue, 0.72f); }
                lyricShadow.fontSize = lyricMain.fontSize;
                GUI.Label(new Rect(bounds.x + 32, bounds.y + 1, width - 62, lyricMainHeight), currentText, lyricShadow);
                GUI.Label(new Rect(bounds.x + 31, bounds.y, width - 62, lyricMainHeight), currentText, lyricMain);
                Label(new Rect(bounds.x + 41, bounds.y + 3 + lyricMainHeight, width - 80, lyricNextHeight), nextText, lyricNextShadow);
                Label(new Rect(bounds.x + 40, bounds.y + 2 + lyricMainHeight, width - 80, lyricNextHeight), nextText, lyricNext);
                GUI.DragWindow(new Rect(20, 0, bounds.width - 40, bounds.height));
        }
        void FlushLyricDrag()
        {
            if (!lyricDragPending || lyricsX == null || lyricsY == null) return;
            float scale = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            lyricsX.Value = Mathf.Clamp01((lyricOverlayRect.x - 12) / Mathf.Max(1, Screen.width / scale - lyricOverlayRect.width - 24));
            lyricsY.Value = Mathf.Clamp01((lyricOverlayRect.y - 12) / Mathf.Max(1, Screen.height / scale - lyricOverlayRect.height - 24));
            lyricDragPending = false; Config.Save();
        }
        void MoveLyricsVertical(float delta)
        {
            FlushLyricDrag();
            if (lyricsY.Value < 0) lyricsBottom.Value = Mathf.Clamp(lyricsBottom.Value + delta, 30, 360);
            else
            {
                float scale = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
                lyricsY.Value = Mathf.Clamp01(lyricsY.Value - delta / Mathf.Max(1, Screen.height / scale - lyricOverlayRect.height - 24));
            }
            Config.Save();
        }
        void DrawLyricsSettings(int id)
        {
            float width = lyricsSettingsRect.width;
            GUI.Label(new Rect(24, 17, width - 100, 34), "歌词  ·  喜马拉雅", headingText);
            if (GUI.Button(new Rect(width - 72, 18, 50, 30), "收起", quietButton)) lyricsSettingsOpen = false;
            Label(new Rect(25, 56, width - 50, 26), lyricTrack == null ? "先播放一首本平台音频，再导入歌词" : lyricTrack.Title, bodyText);
            if (GUI.Button(new Rect(24, 96, 145, 34), lyricsEnabled.Value ? "歌词：显示（V）" : "歌词：隐藏（V）", buttonStyle)) ToggleLyrics();
            if (GUI.Button(new Rect(181, 96, 82, 34), "字号 −", buttonStyle)) { lyricsSize.Value = Math.Max(14, lyricsSize.Value - 2); Config.Save(); }
            if (GUI.Button(new Rect(270, 96, 82, 34), "字号 ＋", buttonStyle)) { lyricsSize.Value = Math.Min(34, lyricsSize.Value + 2); Config.Save(); }
            if (GUI.Button(new Rect(364, 96, 92, 34), "位置上移", buttonStyle)) MoveLyricsVertical(10);
            if (GUI.Button(new Rect(464, 96, 92, 34), "位置下移", buttonStyle)) MoveLyricsVertical(-10);
            if (GUI.Button(new Rect(566, 96, width - 590, 34), "重置位置", buttonStyle)) { lyricDragPending = false; lyricsX.Value = lyricsY.Value = -1; lyricsBottom.Value = 110; Config.Save(); }
            if (GUI.Button(new Rect(24, 143, 130, 34), "延后 0.5 秒", buttonStyle)) { lyricsOffset.Value = Math.Max(-15, lyricsOffset.Value - 0.5f); Config.Save(); }
            if (GUI.Button(new Rect(165, 143, 130, 34), "提前 0.5 秒", buttonStyle)) { lyricsOffset.Value = Math.Min(15, lyricsOffset.Value + 0.5f); Config.Save(); }
            GUI.Label(new Rect(310, 147, width - 340, 28), "同步修正 " + lyricsOffset.Value.ToString("+0.0;-0.0;0.0") + " 秒", mutedText);
            GUI.Label(new Rect(24, 186, 68, 30), "歌词颜色", mutedText);
            for (int i = 0; i < LyricPalette.Names.Length; i++)
                if (GUI.Button(new Rect(96 + i * 66, 184, 60, 32), LyricPalette.Names[i], string.Equals(lyricsColor.Value, LyricPalette.Hex[i], StringComparison.OrdinalIgnoreCase) ? primaryButton : buttonStyle))
                { lyricsColor.Value = lyricColorInput = LyricPalette.Hex[i]; Config.Save(); }
            GUI.SetNextControlName("lyric-color");
            lyricColorInput = GUI.TextField(new Rect(502, 184, 130, 32), lyricColorInput, 7, inputStyle);
            if (GUI.Button(new Rect(642, 184, 110, 32), "应用颜色", buttonStyle))
            {
                float red, green, blue;
                if (LyricPalette.TryRgb(lyricColorInput, out red, out green, out blue)) { lyricsColor.Value = lyricColorInput.ToUpperInvariant(); Config.Save(); }
                else lyricsMessage = "颜色请使用 #RRGGBB，例如 #AADFFF。";
            }
            GUI.Label(new Rect(25, 235, width - 50, 28), "导入当前音频的 LRC 文件，或粘贴带时间戳的歌词", bodyText);
            string file = Input(new Rect(24, 270, width - 180, 36), lyricsFile, "lyric-file", @"C:\Music\歌曲.lrc");
            if (file != lyricsFile) { lyricsFile = file; lyricsImportKey = lyricTrack == null ? null : lyricTrack.Key; }
            GUI.enabled = lyricTrack != null && lyricTask == null;
            if (GUI.Button(new Rect(width - 143, 270, 119, 36), "导入歌词", primaryButton)) RequestLyrics(true);
            GUI.enabled = true;
            GUI.SetNextControlName("lyric-paste");
            string pasted = GUI.TextArea(new Rect(24, 317, width - 48, 80), lyricsPaste, inputStyle);
            if (pasted.Length > LyricDocument.MaxCharacters) pasted = pasted.Substring(0, LyricDocument.MaxCharacters);
            if (pasted != lyricsPaste) { lyricsPaste = pasted; lyricsImportKey = lyricTrack == null ? null : lyricTrack.Key; }
            var hint = new GUIStyle(mutedText); hint.wordWrap = true;
            GUI.Label(new Rect(25, 408, width - 190, 43), lyricsMessage, hint);
            GUI.enabled = lyricTrack != null && lyricTask == null;
            if (GUI.Button(new Rect(width - 154, 409, 130, 34), "重新获取", buttonStyle)) RequestLyrics(false);
            GUI.enabled = true;
            float listHeight = Mathf.Max(60, lyricsSettingsRect.height - 499);
            GUI.Box(new Rect(24, 461, width - 48, listHeight), "", cardStyle);
            lyricListScroll = GUI.BeginScrollView(new Rect(30, 467, width - 60, listHeight - 12), lyricListScroll, new Rect(0, 0, width - 88, Math.Max(listHeight - 16, lyricDocument.Lines.Count * 34)));
            int selected = lyricDocument.IndexAt(lyricPosition + lyricsOffset.Value);
            int first = Math.Max(0, (int)(lyricListScroll.y / 34));
            int end = Math.Min(lyricDocument.Lines.Count, first + (int)(listHeight / 34) + 2);
            for (int i = first; i < end; i++) Label(new Rect(8, i * 34, width - 110, 30), lyricDocument.Lines[i].Text.Replace('\n', ' '), i == selected ? accentText : mutedText);
            GUI.EndScrollView();
            GUI.Label(new Rect(25, lyricsSettingsRect.height - 29, width - 50, 22), "拖动歌词可保存位置；V 显示 / 隐藏；歌词仅保存在本机。", smallText);
            GUI.DragWindow(new Rect(0, 0, width - 90, 50));
        }
        void LyricsGuiFocus() { lyricTyping = (visible || lyricsSettingsOpen) && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()); }
        void DisposeLyricsUI() { lyricMain = null; lyricNext = null; lyricCaption = null; lyricShadow = null; lyricNextShadow = null; lyricWindowStyle = null; }
    }
}
