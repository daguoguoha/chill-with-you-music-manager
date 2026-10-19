// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ChillSaltPlayerPlaylist {
    public sealed partial class Plugin {
        GameObject nativeHeartObject;
        Button nativeHeartButton;
        NativeHeartGraphic nativeHeartIcon;
        Image nativeHeartSourceImage;
        RectTransform nativeHeartRect, nativeHeartReference;
        float nativeHeartScan;
        bool nativeHeartFailed;
        bool nativeFavoriteNotify;

        void NativeFavoriteTick() {
            if(destroying || nativeHeartFailed)return;
            try {
                if(nativeHeartObject==null) {
                    if(Time.realtimeSinceStartup<nativeHeartScan)return;
                    nativeHeartScan=Time.realtimeSinceStartup+1;
                    var ui=Resources.FindObjectsOfTypeAll<MusicUI>().FirstOrDefault(x=>x.gameObject.scene.IsValid());
                    if(ui==null)return;
                    var reference=AccessTools.Field(typeof(MusicUI),"_loopButton").GetValue(ui) as Button;
                    if(reference==null || reference.transform.parent==null)return;
                    nativeHeartReference=reference.transform as RectTransform;
                    nativeHeartSourceImage=AccessTools.Field(typeof(MusicUI),"_loopChangeButtonImage").GetValue(ui) as Image;
                    nativeHeartObject=new GameObject("saltplayerAccountHeart",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(LayoutElement));
                    nativeHeartObject.transform.SetParent(reference.transform.parent,false);
                    nativeHeartObject.GetComponent<LayoutElement>().ignoreLayout=true;
                    nativeHeartRect=nativeHeartObject.GetComponent<RectTransform>();
                    var hit=nativeHeartObject.GetComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                    nativeHeartButton=nativeHeartObject.GetComponent<Button>();
                    var label=new GameObject("Heart",typeof(RectTransform),typeof(CanvasRenderer),typeof(NativeHeartGraphic));
                    label.transform.SetParent(nativeHeartObject.transform,false);
                    var textRect=label.GetComponent<RectTransform>();textRect.anchorMin=textRect.anchorMax=textRect.pivot=new Vector2(0.5f,0.5f);
                    nativeHeartIcon=label.GetComponent<NativeHeartGraphic>();nativeHeartIcon.raycastTarget=false;
                    nativeHeartButton.targetGraphic=nativeHeartIcon;
                    nativeHeartButton.onClick.AddListener(NativeFavoriteClick);
                    Logger.LogInfo("底部原生播放栏账号红心已创建。");
                }
                var service=NativeService;Song song=null;
                bool applicable=service!=null && service.PlayingMusic!=null && IsCloud(service.PlayingMusic) && nativeSongs.TryGetValue(service.PlayingMusic.UUID,out song);
                nativeHeartObject.SetActive(applicable);
                if(!applicable)return;
                // Follow the existing loop button across window and UI scale changes.
                nativeHeartRect.anchorMin=nativeHeartReference.anchorMin;nativeHeartRect.anchorMax=nativeHeartReference.anchorMax;nativeHeartRect.pivot=nativeHeartReference.pivot;
                float width=nativeHeartReference.rect.width;
                nativeHeartRect.sizeDelta=nativeHeartReference.rect.size;
                nativeHeartRect.anchoredPosition=nativeHeartReference.anchoredPosition+new Vector2((nativeHeartReference.rect.width+width)*0.5f+12,0);
                var sourceIcon=nativeHeartSourceImage==null?nativeHeartReference:nativeHeartSourceImage.rectTransform;
                var localCenter=nativeHeartReference.InverseTransformPoint(sourceIcon.TransformPoint(sourceIcon.rect.center));
                nativeHeartIcon.rectTransform.anchoredPosition=(Vector2)localCenter-nativeHeartReference.rect.center;
                var corners=new Vector3[4];sourceIcon.GetWorldCorners(corners);
                var lower=nativeHeartReference.InverseTransformPoint(corners[0]);var upper=nativeHeartReference.InverseTransformPoint(corners[2]);
                nativeHeartIcon.rectTransform.sizeDelta=new Vector2(Mathf.Abs(upper.x-lower.x),Mathf.Abs(upper.y-lower.y));
                bool liked=FavoriteKnown(song) && favoriteIds.Contains(FavoriteKey(song));
                nativeHeartIcon.Filled=liked;
                nativeHeartButton.interactable=CanFavorite && favoriteJob==null;
                var colors=nativeHeartButton.colors;
                colors.normalColor=liked?new Color(0.96f,0.42f,0.58f,1):new Color(0.89f,0.85f,0.96f,1);
                colors.highlightedColor=new Color(1f,0.66f,0.78f,1);colors.pressedColor=new Color(0.95f,0.35f,0.52f,1);colors.selectedColor=colors.normalColor;colors.disabledColor=new Color(0.62f,0.58f,0.69f,0.7f);
                nativeHeartButton.colors=colors;
            } catch(Exception ex) {
                nativeHeartFailed=true;
                if(nativeHeartObject!=null)Destroy(nativeHeartObject);
                Logger.LogWarning("底部红心暂不可用："+ex.GetType().Name);
            }
        }
        void NativeFavoriteClick() {
            var service=NativeService;Song song;
            if(destroying || !CanFavorite || favoriteJob!=null || service==null || service.PlayingMusic==null || !IsCloud(service.PlayingMusic) || !nativeSongs.TryGetValue(service.PlayingMusic.UUID,out song))return;
            nativeFavoriteNotify=true;
            if(!FavoriteKnown(song)){ReadFavorite(song);NativeNotice("正在读取收藏状态，请稍后再点红心。",4);return;}
            StartFavoriteJob(song,!favoriteIds.Contains(FavoriteKey(song)));
        }
        void NativeFavoriteOutcome(string message){if(nativeFavoriteNotify){nativeFavoriteNotify=false;NativeNotice(message,4);}}
        void CloseNativeFavorite() {
            if(nativeHeartObject!=null)Destroy(nativeHeartObject);
        }
    }
}
