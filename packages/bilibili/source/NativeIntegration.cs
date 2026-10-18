// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bulbul;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChillBilibiliPlaylist
{
    public sealed partial class Plugin
    {
        const AudioTag CloudTag = (AudioTag)(1 << 23);
        const string CloudPrefix = "bilibili:";
        readonly object nativeGate = new object();
        readonly Dictionary<string, Song> nativeSongs = new Dictionary<string, Song>();
        readonly List<GameAudioInfo> nativeClips = new List<GameAudioInfo>();
        readonly Dictionary<AudioClip, DecodedAudio> streamingClips = new Dictionary<AudioClip, DecodedAudio>();
        readonly List<GameObject> nativeButtons = new List<GameObject>();
        MusicTagListUI nativeTagUI;
        int nativeMenuVersion = -1, nativeLoadVersion;
        bool nativeDelivering;
        float nativeNextCheck, nativeNoticeUntil;
        void NativeNotice(string message, float seconds)
        {
            status = message;
            nativeNoticeUntil = Time.realtimeSinceStartup + seconds;
        }
        static bool IsCloud(GameAudioInfo info) { return info != null && info.UUID != null && info.UUID.StartsWith(CloudPrefix, StringComparison.Ordinal); }
        MusicService NativeService { get { return MusicService as MusicService; } }

        void SetupNative()
        {
            harmony.Patch(AccessTools.Method(typeof(GameAudioInfo), "GetAudioClip"), prefix: new HarmonyMethod(typeof(Plugin), "NativeGetClip"));
            harmony.Patch(AccessTools.Method(typeof(MusicService), "PlayArugumentMusic"), prefix: new HarmonyMethod(typeof(Plugin), "NativePlayArgument"));
            harmony.Patch(AccessTools.Method(typeof(MusicService), "PlayMusicInPlaylist"), prefix: new HarmonyMethod(typeof(Plugin), "NativePlayIndex"));
            foreach (string name in new[] { "PlayNextMusic", "PlayBackMusic", "Stop" })
                harmony.Patch(AccessTools.Method(typeof(MusicService), name), prefix: new HarmonyMethod(typeof(Plugin), "NativeCancelSelection"));
            harmony.Patch(AccessTools.Method(typeof(MusicTagListUI), "Setup"), postfix: new HarmonyMethod(typeof(Plugin), "NativeTagSetup"));
            harmony.Patch(AccessTools.Method(typeof(MusicTagListUI), "SetTitle"), prefix: new HarmonyMethod(typeof(Plugin), "NativeTagTitle"));
            harmony.Patch(AccessTools.Method(typeof(MusicTagListUI), "<Setup>b__7_1"), prefix: new HarmonyMethod(typeof(Plugin), "NativeOriginalTag"));
            harmony.Patch(AccessTools.Method(typeof(MusicSettingV2), "SaveReady"), postfix: new HarmonyMethod(typeof(Plugin), "NativeSaveReady"));
            // These three lists belong to the game save. Cloud IDs remain runtime-only.
            harmony.Patch(AccessTools.Method(typeof(SaveDataManager), "SaveMusicSetting"), prefix: new HarmonyMethod(typeof(Plugin), "NativeSavePrefix"), finalizer: new HarmonyMethod(typeof(Plugin), "NativeSaveFinalizer"));
            Logger.LogInfo("原生B站播放器接口已接入。");
        }
        static void NativeTagSetup(MusicTagListUI __instance) { if (Instance != null) { Instance.nativeTagUI = __instance; Instance.nativeMenuVersion = -1; } }
        static bool NativeTagTitle(MusicTagListUI __instance, AudioTag? __0)
        {
            if (Instance == null || !SaveDataManager.HasInstance) return true;
            AudioTag tag = __0 ?? SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value;
            if ((tag & CloudTag) == 0) return true;
            object pulldown = AccessTools.Field(typeof(MusicTagListUI), "_pulldown").GetValue(__instance);
            AccessTools.Method(pulldown.GetType(), "ChangeSelectContentText").Invoke(pulldown, new object[] { "B站 · " + Instance.selectedPlaylistTitle });
            return false;
        }
        static void NativeOriginalTag()
        {
            if (SaveDataManager.HasInstance && (SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value & CloudTag) != 0)
                SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = (AudioTag)0;
        }
        static void NativeSaveReady(MusicSettingV2 __instance)
        {
            AudioTag tag = (AudioTag)AccessTools.Field(typeof(MusicSettingV2), "_audioTagForSave").GetValue(__instance);
            if ((tag & CloudTag) != 0) AccessTools.Field(typeof(MusicSettingV2), "_audioTagForSave").SetValue(__instance, AudioTag.All);
            __instance.PlaylistOrder.RemoveAll(IsCloudId);
            __instance.FavoriteAudioUUIDs.RemoveAll(IsCloudId);
            __instance.ExcludedFromPlaylistUUIDs.RemoveAll(IsCloudId);
        }
        static bool IsCloudId(string id) { return id != null && id.StartsWith(CloudPrefix, StringComparison.Ordinal); }
        sealed class CloudSaveState { public MusicSettingV2 Setting; public List<string> Order, Favorite, Excluded; }
        static void NativeSavePrefix(out CloudSaveState __state)
        {
            __state = null;
            if (!SaveDataManager.HasInstance) return;
            MusicSettingV2 s = SaveDataManager.Instance.MusicSetting;
            if (s == null) return;
            __state = new CloudSaveState { Setting = s, Order = s.PlaylistOrder, Favorite = s.FavoriteAudioUUIDs, Excluded = s.ExcludedFromPlaylistUUIDs };
            s.PlaylistOrder = s.PlaylistOrder.Where(id => !IsCloudId(id)).ToList();
            s.FavoriteAudioUUIDs = s.FavoriteAudioUUIDs.Where(id => !IsCloudId(id)).ToList();
            s.ExcludedFromPlaylistUUIDs = s.ExcludedFromPlaylistUUIDs.Where(id => !IsCloudId(id)).ToList();
        }
        static Exception NativeSaveFinalizer(Exception __exception, CloudSaveState __state)
        {
            if (__state != null) { __state.Setting.PlaylistOrder = __state.Order; __state.Setting.FavoriteAudioUUIDs = __state.Favorite; __state.Setting.ExcludedFromPlaylistUUIDs = __state.Excluded; }
            return __exception;
        }
        static bool NativeGetClip(GameAudioInfo __instance, CancellationToken __0, ref UniTask<AudioClip> __result)
        {
            if (!IsCloud(__instance) || Instance == null) return true;
            // The game's next/back queue advances only when an unavailable clip returns null.
            // An exception here terminates its async loop and leaves the previous song playing.
            UniTaskCompletionSource<AudioClip> completion = new UniTaskCompletionSource<AudioClip>();
            Instance.StartCoroutine(Instance.NativeQueueClip(__instance, __0, completion));
            __result = completion.Task;
            return false;
        }
        IEnumerator NativeQueueClip(GameAudioInfo info, CancellationToken cancellation, UniTaskCompletionSource<AudioClip> completion)
        {
            UniTask<AudioClip> loading = BeginNativeLoad(info, cancellation);
            int version = nativeLoadVersion;
            while (loading.Status == UniTaskStatus.Pending) yield return null;
            AudioClip clip = null;
            try { clip = loading.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { completion.TrySetCanceled(); yield break; }
            catch (Exception error)
            {
                if (version == nativeLoadVersion)
                    NativeNotice("已跳过「" + info.Title + "」：" + error.Message, 8);
                Logger.LogWarning("原生切歌跳过不可用歌曲：" + info.Title);
            }
            completion.TrySetResult(clip);
        }
        static bool NativePlayArgument(MusicService __instance, GameAudioInfo __0, MusicChangeKind __1)
        {
            if (Instance == null || Instance.nativeDelivering) return true;
            if (!IsCloud(__0)) { Instance.nativeLoadVersion++; return true; }
            if (__0.AudioClip != null) { Instance.nativeLoadVersion++; return true; }
            Instance.StartCoroutine(Instance.NativeSelect(__0, __1));
            return false;
        }
        static bool NativePlayIndex(MusicService __instance, int __0, ref bool __result)
        {
            if (Instance == null || __0 < 0 || __0 >= __instance.CurrentPlayList.Count) return true;
            GameAudioInfo info = __instance.CurrentPlayList[__0];
            if (!IsCloud(info)) { Instance.nativeLoadVersion++; return true; }
            Instance.StartCoroutine(Instance.NativeSelect(info, (MusicChangeKind)1));
            __result = true;
            return false;
        }
        static void NativeCancelSelection() { if (Instance != null && !Instance.nativeDelivering) Instance.nativeLoadVersion++; }
        IEnumerator NativeSelect(GameAudioInfo info, MusicChangeKind kind)
        {
            MusicService service = NativeService;
            if (service == null) yield break;
            CancellationTokenSource previous = AccessTools.Field(typeof(MusicService), "audioLoadCancellation").GetValue(service) as CancellationTokenSource;
            if (previous != null) { try { previous.Cancel(); } catch (ObjectDisposedException) { } }
            UniTask<AudioClip> loading = BeginNativeLoad(info, CancellationToken.None);
            int version = nativeLoadVersion;
            while (loading.Status == UniTaskStatus.Pending) yield return null;
            AudioClip result = null;
            try { result = loading.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { yield break; }
            catch (Exception ex)
            {
                if (version == nativeLoadVersion)
                    NativeNotice("无法播放「" + info.Title + "」：" + ex.Message, 10);
                Logger.LogWarning("原生音源加载失败：" + info.Title + " · " + ex.Message);
                yield break;
            }
            if (result == null || nativeLoadVersion != version || destroying) yield break;
            nativeDelivering = true;
            try { service.PlayArugumentMusic(info, kind); service.IsPause = false; }
            finally { nativeDelivering = false; }
            Logger.LogInfo("原生播放器开始播放B站歌曲：" + info.Title);
        }
        UniTask<AudioClip> BeginNativeLoad(GameAudioInfo info, CancellationToken cancellation)
        {
            int version = ++nativeLoadVersion;
            if (info.AudioClip != null) return UniTask.FromResult(info.AudioClip);
            UniTaskCompletionSource<AudioClip> completion = new UniTaskCompletionSource<AudioClip>();
            StartCoroutine(NativeLoadClip(info, cancellation, version, completion));
            return completion.Task;
        }
        IEnumerator NativeLoadClip(GameAudioInfo info, CancellationToken cancellation, int version, UniTaskCompletionSource<AudioClip> completion)
        {
            Song song;
            if (!nativeSongs.TryGetValue(info.UUID, out song)) { completion.TrySetException(new InvalidOperationException("歌单条目已过期。")); yield break; }
            NativeNotice("正在加载「" + song.name + "」…", 60);
            Task<SongUrl> urlTask = Task.Factory.StartNew(delegate { lock (nativeGate) { return JsonConvert.DeserializeObject<SongUrl>(Bridge.SongURL(song.id)); } });
            while (!urlTask.IsCompleted)
            {
                if (destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion) { completion.TrySetCanceled(); yield break; }
                yield return null;
            }
            if (destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion) { completion.TrySetCanceled(); yield break; }
            if (urlTask.IsFaulted) { completion.TrySetException(urlTask.Exception.GetBaseException()); yield break; }
            SongUrl url = urlTask.Result;
            string format = url == null ? "" : (url.type ?? "").ToLowerInvariant();
            if (url == null || !Rules.IsMusicUrl(url.url) || (format != "mp3" && format != "m4a" && format != "aac" && format != "flac") || url.size > 64L * 1024 * 1024)
            { completion.TrySetException(new InvalidOperationException("歌曲没有可用音源，请确认账号权限。")); yield break; }
            byte[] encoded;
            using (UnityEngine.Networking.UnityWebRequest download = UnityEngine.Networking.UnityWebRequest.Get(Rules.SecureMusicUrl(url.url)))
            {
                download.timeout = 45;
                download.SetRequestHeader("Referer", "https://www.bilibili.com/");
                download.SetRequestHeader("User-Agent", Bridge.UserAgent);
                UnityEngine.Networking.UnityWebRequestAsyncOperation operation;
                try { operation = download.SendWebRequest(); }
                catch (Exception ex) { completion.TrySetException(ex); yield break; }
                while (!operation.isDone)
                {
                    if (destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion)
                    { download.Abort(); completion.TrySetCanceled(); yield break; }
                    if (download.downloadedBytes > 64UL * 1024 * 1024)
                    { download.Abort(); completion.TrySetException(new InvalidOperationException("音频超过 64 MB。")); yield break; }
                    yield return null;
                }
                if (destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion)
                { completion.TrySetCanceled(); yield break; }
                if (download.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                { completion.TrySetException(new InvalidOperationException("音频下载失败。")); yield break; }
                encoded = download.downloadHandler.data;
            }
            Task<DecodedAudio> decode = Task.Factory.StartNew(delegate { return AudioDecoder.Decode(encoded, format, delegate { return destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion; }); });
            // Observe completion even when the user selects another song; the worker deletes its temporary file.
            while (!decode.IsCompleted) yield return null;
            if (decode.IsFaulted)
            {
                Exception error = decode.Exception.GetBaseException();
                if (error is OperationCanceledException) completion.TrySetCanceled();
                else if (error is InvalidOperationException) completion.TrySetException(new InvalidOperationException(error.Message));
                else if ((uint)error.HResult == 0x80070070) completion.TrySetException(new InvalidOperationException("系统临时目录空间不足，请清理空间后重试。"));
                else completion.TrySetException(new InvalidOperationException("Windows 无法解码此视频音轨（0x" + error.HResult.ToString("X8") + "）。"));
                yield break;
            }
            DecodedAudio pcm = decode.Result;
            if (destroying || cancellation.IsCancellationRequested || version != nativeLoadVersion)
            { pcm.Dispose(); completion.TrySetCanceled(); yield break; }
            AudioClip clip = null;
            try
            {
                if (pcm.IsStreaming)
                {
                    clip = AudioClip.Create(CloudPrefix + song.id, pcm.Frames, pcm.Channels, pcm.SampleRate, true, pcm.Read, pcm.Seek);
                    if (clip == null) throw new InvalidOperationException("游戏无法创建流式音频。");
                    streamingClips.Add(clip, pcm);
                }
                else
                {
                    clip = AudioClip.Create(CloudPrefix + song.id, pcm.Frames, pcm.Channels, pcm.SampleRate, false);
                    if (clip == null || !clip.SetData(pcm.Samples, 0)) throw new InvalidOperationException("游戏无法接收解码音频。");
                    pcm.Dispose();
                }
            }
            catch (Exception)
            {
                if (clip != null) { streamingClips.Remove(clip); Destroy(clip); }
                pcm.Dispose(); completion.TrySetException(new InvalidOperationException("游戏无法创建此视频音频，请重新点播。")); yield break;
            }
            clip.name = CloudPrefix + song.id;
            info.AudioClip = clip;
            nativeClips.Remove(info); nativeClips.Add(info);
            for (int i = 0; (nativeClips.Count > 4 || streamingClips.Count > 2) && i < nativeClips.Count; )
            {
                GameAudioInfo old = nativeClips[i];
                if (old == info || (NativeService != null && NativeService.PlayingMusic == old)) { i++; continue; }
                if (nativeClips.Count <= 4 && old.AudioClip != null && !streamingClips.ContainsKey(old.AudioClip)) { i++; continue; }
                ReleaseNativeClip(old); nativeClips.RemoveAt(i);
            }
            NativeNotice("正在播放 · " + song.name, 4);
            completion.TrySetResult(clip);
        }
        void ReleaseNativeClip(GameAudioInfo info)
        {
            AudioClip clip = info.AudioClip;
            if (!object.ReferenceEquals(clip, null))
            {
                DecodedAudio source;
                if (streamingClips.TryGetValue(clip, out source)) { streamingClips.Remove(clip); source.Dispose(); }
                if (clip != null) Destroy(clip);
            }
            info.AudioClip = null;
        }
        void RegisterNativePlaylist()
        {
            MusicService service = NativeService;
            if (service == null || !SaveDataManager.HasInstance) return;
            List<GameAudioInfo> all = AccessTools.Field(typeof(MusicService), "_allMusicList").GetValue(service) as List<GameAudioInfo>;
            if (all == null) return;
            nativeLoadVersion++;
            all.RemoveAll(IsCloud);
            nativeSongs.Clear();
            foreach (Song song in songs)
            {
                string id = CloudPrefix + song.id;
                nativeSongs[id] = song;
                listeningSources[id] = Convert.ToString(selectedPlaylistId);
                GameAudioInfo info = nativeClips.Find(a => a.UUID == id);
                if (info == null) info = GameAudioInfo.CreateNormal(null, CloudTag, song.name, Artists(song), id, false, "", "");
                info.IsUnlocked = true;
                all.Add(info);
            }
            // A new tag rebuilds the native observable playlist and the existing native rows.
            AccessTools.Field(typeof(MusicService), "shuffleList").SetValue(service, all.ToList());
            SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = (AudioTag)0;
            SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = CloudTag;
            if (nativeTagUI != null) AccessTools.Method(typeof(MusicTagListUI), "SetTitle").Invoke(nativeTagUI, new object[] { (AudioTag?)CloudTag });
            service.SetIsAllExcludedMusicFromPlaylist();
            Logger.LogInfo("原生B站歌单已载入：" + selectedPlaylistTitle + "，" + songs.Count + " 首。");
        }
        void NativeTick()
        {
            if (Time.realtimeSinceStartup < nativeNextCheck) return;
            nativeNextCheck = Time.realtimeSinceStartup + 0.5f;
            if (nativeTagUI == null) nativeTagUI = Resources.FindObjectsOfTypeAll<MusicTagListUI>().FirstOrDefault(a => a.gameObject.scene.IsValid());
            if (nativeTagUI != null && ready && playlists.Count > 0 && nativeMenuVersion != playlists.Count)
            { BuildNativeMenu(); nativeMenuVersion = playlists.Count; }
            for (int i = 0; i < nativeButtons.Count && i < playlists.Count; i++)
            {
                if (nativeButtons[i] == null) continue;
                MusicTagListButton button = nativeButtons[i].GetComponent<MusicTagListButton>();
                if (button != null) button.SetCheck(selectedPlaylistId == playlists[i].id && SaveDataManager.HasInstance && (SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value & CloudTag) != 0);
                TMP_Text label = nativeButtons[i].transform.Find("Buttons/TagName").GetComponent<TMP_Text>();
                label.text = "B站 · " + playlists[i].name;
            }
        }
        void BuildNativeMenu()
        {
            MusicTagListButton[] originals = (MusicTagListButton[])AccessTools.Field(typeof(MusicTagListUI), "buttons").GetValue(nativeTagUI);
            if (originals == null || originals.Length == 0) return;
            foreach (GameObject old in nativeButtons) if (old != null) Destroy(old);
            nativeButtons.Clear();
            Transform content = originals[0].transform.parent;
            foreach (Playlist playlist in playlists)
            {
                Playlist item = playlist;
                GameObject row = Instantiate(originals[0].gameObject, content);
                row.name = "BilibiliPlaylist_" + item.id;
                row.SetActive(true);
                Button click = row.GetComponent<Button>();
                if (click == null) click = row.GetComponentInChildren<Button>(true);
                click.onClick.RemoveAllListeners();
                click.onClick.AddListener(delegate
                {
                    if (busy) return;
                    OpenPlaylist(item.id, item.name);
                    nativeTagUI.CloseMusicTagList(false);
                });
                TMP_Text label = row.transform.Find("Buttons/TagName").GetComponent<TMP_Text>();
                foreach (MonoBehaviour behaviour in label.GetComponents<MonoBehaviour>()) if (!(behaviour is TMP_Text)) behaviour.enabled = false;
                label.text = "B站 · " + item.name;
                label.enableAutoSizing = false; label.overflowMode = TextOverflowModes.Ellipsis;
                nativeButtons.Add(row);
            }
            object pulldown = AccessTools.Field(typeof(MusicTagListUI), "_pulldown").GetValue(nativeTagUI);
            RectTransform outer = (RectTransform)AccessTools.Field(pulldown.GetType(), "_pullDownParentRect").GetValue(pulldown);
            if (outer.GetComponent<ScrollRect>() == null)
            {
                GameObject viewport = new GameObject("BilibiliViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(outer, false);
                RectTransform v = viewport.GetComponent<RectTransform>();
                v.anchorMin = Vector2.zero; v.anchorMax = Vector2.one; v.offsetMin = new Vector2(24, 15); v.offsetMax = new Vector2(-24, -60);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;
                content.SetParent(v, false);
                RectTransform c = (RectTransform)content;
                c.anchorMin = new Vector2(0, 1); c.anchorMax = Vector2.one; c.pivot = new Vector2(0.5f, 1); c.anchoredPosition = Vector2.zero; c.sizeDelta = Vector2.zero;
                ContentSizeFitter fit = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect scroll = outer.GetComponent<ScrollRect>() ?? outer.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = v; scroll.content = c; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
                AccessTools.Field(pulldown.GetType(), "_openPullDownSizeDeltaY").SetValue(pulldown, 420f);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            Logger.LogInfo("原生下拉列表已加入 " + nativeButtons.Count + " 个B站歌单。");
        }
        void NativePlayFromPanel(Song[] list, int index)
        {
            SessionManualSelection();
            if (busy || index < 0 || index >= list.Length) return;
            if (songs.Count != list.Length || NativeService == null || !NativeService.AllMusicList.Any(IsCloud)) RegisterNativePlaylist();
            GameAudioInfo info = NativeService == null ? null : NativeService.AllMusicList.FirstOrDefault(a => a.UUID == CloudPrefix + list[index].id);
            if (info == null) return;
            queue = list; current = index; currentName = list[index].Label;
            StartCoroutine(NativeSelect(info, (MusicChangeKind)1));
            visible = false;
        }
    }
}
