// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace ChillNeteasePlaylist {
    public sealed partial class Plugin {
        sealed class FavoriteResult { public HashSet<string> Ids; public string Message; }
        HashSet<string> favoriteIds = new HashSet<string>(StringComparer.Ordinal);
        Task<FavoriteResult> favoriteJob;
        int favoriteEpoch, favoriteJobEpoch;
        bool favoritesKnown;
        GUIStyle heartStyle;
        string FavoriteKey(Song song) { return Bridge.FavoriteKey(song); }
        void ResetFavorites() { favoriteEpoch++; favoritesKnown=false; favoriteIds.Clear(); nativeFavoriteNotify=false; }
        void RefreshFavorites() {
            if (!logged || !ready || destroying || favoriteJob!=null) return;
            StartFavoriteJob(null,false);
        }
        void StartFavoriteJob(Song song,bool desired) {
            if (favoriteJob!=null || !logged || !ready || destroying) return;
            int epoch=favoriteEpoch; favoriteJobEpoch=epoch;
            favoriteJob=Task.Factory.StartNew(delegate {
                lock(nativeGate) {
                    if(epoch!=favoriteEpoch || destroying) throw new OperationCanceledException();
                    string error=null;
                    if(song!=null) {
                        try { Bridge.SetFavorite(song,desired); }
                        catch(Exception) { error="平台未确认更改，已重新读取喜欢状态；请稍后再试。"; }
                    }
                    var ids=Bridge.GetFavoriteIds();
                    if(song!=null && (error!=null || ids.Contains(FavoriteKey(song))!=desired))
                        return new FavoriteResult{Ids=ids,Message=error??"平台状态尚未同步，请刷新喜欢状态后确认。"};
                    return new FavoriteResult{Ids=ids,Message=song==null?"喜欢状态已刷新。":desired?"已同步到账号：我喜欢。":"已从账号的我喜欢中移除。"};
                }
            });
        }
        void FavoritesTick() {
            if(favoriteJob==null || !favoriteJob.IsCompleted)return;
            var task=favoriteJob;favoriteJob=null;
            if(task.IsFaulted){var observed=task.Exception; if(favoriteJobEpoch==favoriteEpoch){favoritesKnown=false;status="喜欢状态读取失败，点击歌曲旁的空心红心可重试。";NativeFavoriteOutcome(status);}return;}
            if(task.IsCanceled || destroying)return;
            if(favoriteJobEpoch!=favoriteEpoch){RefreshFavorites();return;}
            favoriteIds=task.Result.Ids;favoritesKnown=true;
            status=task.Result.Message;
            NativeFavoriteOutcome(status);
        }
        void DrawHeart(Rect rect,Song song) {
            if(song==null)return;
            if(heartStyle==null){heartStyle=new GUIStyle(quietButton);heartStyle.fontSize=23;heartStyle.normal.textColor=new Color(0.96f,0.42f,0.58f);heartStyle.hover.textColor=new Color(1f,0.66f,0.75f);}
            bool liked=favoritesKnown && favoriteIds.Contains(FavoriteKey(song));
            bool was=GUI.enabled;GUI.enabled=was && logged && ready && favoriteJob==null;
            string hint=!favoritesKnown?"点击读取账号喜欢状态":liked?"取消账号喜欢":"加入账号的我喜欢";
            if(GUI.Button(rect,new GUIContent(liked?"♥":"♡",hint),heartStyle)) {
                if(!favoritesKnown)RefreshFavorites();else StartFavoriteJob(song,!liked);
            }
            GUI.enabled=was;
        }
        void DrawCurrentHeart() {
            var service=NativeService;if(service==null || service.PlayingMusic==null)return;
            Song song;if(nativeSongs.TryGetValue(service.PlayingMusic.UUID,out song))DrawHeart(new Rect(602,595,40,34),song);
        }
    }
}
