// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections;
using System.Collections.Generic;
using Bulbul;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
namespace ChillXimalayaPlaylist
{
    public sealed partial class Plugin
    {
        int nativeFocusVersion;
        static bool NativeFocusPlaying(MusicPlayListView __instance)
        {
            Plugin owner=Instance;
            if(owner==null || owner.NativeService==null || !IsCloud(owner.NativeService.PlayingMusic)) return true;
            owner.QueuePlaylistFocus(__instance);
            return false;
        }
        static void NativeFocusChanged(MusicUI __instance)
        {
            if(Instance==null) return;
            var view=AccessTools.Field(typeof(MusicUI),"_musicPlayListView").GetValue(__instance) as MusicPlayListView;
            Instance.QueuePlaylistFocus(view);
        }
        static void NativeFocusRebuilt(MusicPlayListView __instance)
        { if(Instance!=null) Instance.QueuePlaylistFocus(__instance); }
        void QueuePlaylistFocus(MusicPlayListView view)
        {
            if(destroying || view==null || !view.gameObject.activeInHierarchy || NativeService==null || !IsCloud(NativeService.PlayingMusic)) return;
            StartCoroutine(FocusPlaylistRow(view,NativeService.PlayingMusic.UUID,++nativeFocusVersion));
        }
        IEnumerator FocusPlaylistRow(MusicPlayListView view,string id,int version)
        {
            // Let native list reconstruction and the opening animation settle once.
            yield return new WaitForSecondsRealtime(0.25f);
            var service=NativeService;
            string playing=service==null || service.PlayingMusic==null ? null : service.PlayingMusic.UUID;
            if(!PlaylistFocusPolicy.CanFocus(version,nativeFocusVersion,id,playing,destroying,view!=null && view.gameObject.activeInHierarchy)) yield break;
            var rows=AccessTools.Field(typeof(MusicPlayListView),"_playListButtonList").GetValue(view) as List<IMusicPlayListButton>;
            var scroll=AccessTools.Field(typeof(MusicPlayListView),"_scrollRect").GetValue(view) as ScrollRect;
            if(rows==null || scroll==null || scroll.content==null) yield break;
            int index=PlaylistFocusPolicy.Find(rows,delegate(IMusicPlayListButton row) { return row==null || row.AudioInfo==null ? null : row.AudioInfo.UUID; },id);
            // A different browsed playlist remains under the user's control.
            if(index<0 || rows[index].RectTransform==null) yield break;
            scroll.StopMovement();
            NestopiSystem.ScrollRectExtensions.ScrollToCenter(scroll,rows[index].RectTransform);
        }
    }
}
