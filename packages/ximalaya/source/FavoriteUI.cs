// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace ChillXimalayaPlaylist {
    public sealed partial class Plugin {
        sealed class FavoriteResult { public string Key, Message; public bool Liked, Changed; }
        readonly HashSet<string> favoriteIds=new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> favoriteKnownIds=new HashSet<string>(StringComparer.Ordinal);
        Task<FavoriteResult> favoriteJob;
        int favoriteEpoch,favoriteJobEpoch;
        string favoriteJobKey="",favoriteObservedKey="";
        GUIStyle heartStyle;
        bool CanFavorite {get{return ready && !destroying && logged && !listeningSwitchingAccount;}}
        string FavoriteKey(Song song){return Bridge.FavoriteKey(song);}
        bool FavoriteKnown(Song song){return favoriteKnownIds.Contains(FavoriteKey(song));}
        void ResetFavorites(){favoriteEpoch++;favoriteIds.Clear();favoriteKnownIds.Clear();favoriteObservedKey="";nativeFavoriteNotify=false;}
        void RefreshFavorites(){ResetFavorites();}
        void ReadFavorite(Song song){StartFavoriteJobCore(song,null);}
        void StartFavoriteJob(Song song,bool desired){StartFavoriteJobCore(song,desired);}
        void StartFavoriteJobCore(Song song,bool? desired) {
            if(!CanFavorite || favoriteJob!=null || song==null)return;
            string key=FavoriteKey(song);if(string.IsNullOrEmpty(key))return;
            favoriteJobKey=key;favoriteJobEpoch=favoriteEpoch;int epoch=favoriteEpoch;
            favoriteJob=Task.Factory.StartNew(delegate {
                lock(nativeGate) {
                    if(epoch!=favoriteEpoch || destroying)throw new OperationCanceledException();
                    var state=FavoriteOperation.Run(()=>Bridge.IsFavorite(song),value=>Bridge.SetFavorite(song,value),desired);
                    string message=state.Unconfirmed?(state.Error+" 已重新读取当前状态。"):desired.HasValue?(state.Liked==desired.Value?(state.Liked?"已加入账号的喜欢的声音。":"已从喜欢的声音中移除。"):"平台状态尚未同步，请稍后刷新确认。"):(state.Liked?"当前已喜欢。":"当前未喜欢。");
                    return new FavoriteResult{Key=key,Liked=state.Liked,Changed=desired.HasValue,Message=message};
                }
            });
        }
        void FavoritesTick() {
            if(favoriteJob!=null && favoriteJob.IsCompleted) {
                var task=favoriteJob;favoriteJob=null;
                if(task.IsFaulted) {
                    var error=task.Exception.GetBaseException();
                    if(favoriteJobEpoch==favoriteEpoch){favoriteKnownIds.Remove(favoriteJobKey);favoriteIds.Remove(favoriteJobKey);status="喜欢状态读取失败，请稍后点击红心重试。"; NativeFavoriteOutcome(status);}
                } else if(!task.IsCanceled && !destroying && favoriteJobEpoch==favoriteEpoch) {
                    var value=task.Result;
                    if(favoriteKnownIds.Count>=10000){favoriteKnownIds.Clear();favoriteIds.Clear();}
                    favoriteKnownIds.Add(value.Key);if(value.Liked)favoriteIds.Add(value.Key);else favoriteIds.Remove(value.Key);
                    status=value.Message;NativeFavoriteOutcome(status);
                }
            }
            if(!CanFavorite || favoriteJob!=null)return;
            var service=NativeService;Song song;
            if(service==null || service.PlayingMusic==null || !IsCloud(service.PlayingMusic) || !nativeSongs.TryGetValue(service.PlayingMusic.UUID,out song))return;
            string key=FavoriteKey(song);
            if(key!=favoriteObservedKey){favoriteObservedKey=key;if(!FavoriteKnown(song))ReadFavorite(song);}
        }
        void DrawHeart(Rect rect,Song song) {
            if(song==null)return;
            if(heartStyle==null){heartStyle=new GUIStyle(quietButton);heartStyle.fontSize=23;heartStyle.normal.textColor=new Color(0.96f,0.42f,0.58f);heartStyle.hover.textColor=new Color(1f,0.66f,0.75f);}
            bool known=FavoriteKnown(song),liked=known && favoriteIds.Contains(FavoriteKey(song));
            bool was=GUI.enabled;GUI.enabled=was && CanFavorite && favoriteJob==null;
            string hint=!known?"点击读取喜欢状态":liked?"取消喜欢":"加入喜欢的声音";
            if(GUI.Button(rect,new GUIContent(liked?"♥":"♡",hint),heartStyle)){if(!known)ReadFavorite(song);else StartFavoriteJob(song,!liked);}
            GUI.enabled=was;
        }
        void DrawCurrentHeart(){var service=NativeService;Song song;if(service!=null && service.PlayingMusic!=null && nativeSongs.TryGetValue(service.PlayingMusic.UUID,out song))DrawHeart(new Rect(602,595,40,34),song);}
    }
}
