// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Bulbul;
using HarmonyLib;
using ObservableCollections;
namespace ChillBilibiliPlaylist
{
    public sealed partial class Plugin
    {
        List<GameAudioInfo> nativeQueueSource;
        GameAudioInfo[] nativeQueueDisplay;
        ObservableList<GameAudioInfo> nativePlaybackOrder;
        static bool OwnPlaylist(MusicService service)
        { return service!=null && service.CurrentPlayList.Count>0 && service.CurrentPlayList.All(IsCloud); }
        static IEnumerable<CodeInstruction> NativeShuffleDisplaySource(IEnumerable<CodeInstruction> instructions)
        {
            var list=instructions.ToList();
            for(int i=0;i<list.Count;i++)
            {
                var instruction=list[i];yield return instruction;
                var field=instruction.operand as FieldInfo;
                int next=i+1;
                while(next<list.Count && list[next].opcode==OpCodes.Call && list[next].operand is MethodInfo
                    && ((MethodInfo)list[next].operand).Name=="NativeDisplaySource") next++;
                if(instruction.opcode==OpCodes.Ldfld && field!=null && field.DeclaringType==typeof(MusicService)
                    && field.Name=="shuffleList" && next<list.Count && list[next].opcode.Name.StartsWith("stloc"))
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Plugin),"NativeDisplaySource"));
            }
        }
        static List<GameAudioInfo> NativeDisplaySource(List<GameAudioInfo> randomized)
        {
            Plugin owner=Instance;
            if(owner==null || owner.NativeService==null || !SaveDataManager.HasInstance
                || (SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value & CloudTag)==0) return randomized;
            return AccessTools.Field(typeof(MusicService),"_allMusicList").GetValue(owner.NativeService) as List<GameAudioInfo> ?? randomized;
        }
        // Keep the game's bounded loading, exclusion, cancellation and end-of-track paths.
        // Each enabled provider wraps the getter independently; only the active one substitutes it.
        static IEnumerable<CodeInstruction> NativeShufflePlayback(IEnumerable<CodeInstruction> instructions)
        {
            var getter=AccessTools.PropertyGetter(typeof(MusicService),"CurrentPlayList");
            foreach(var instruction in instructions)
            {
                yield return instruction;
                if((instruction.opcode==OpCodes.Call || instruction.opcode==OpCodes.Callvirt) && Equals(instruction.operand,getter))
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Plugin),"NativeShuffleRead"));
            }
        }
        static ObservableList<GameAudioInfo> NativeShuffleRead(ObservableList<GameAudioInfo> original)
        {
            Plugin owner=Instance;
            var service=owner==null?null:owner.NativeService;
            if(owner==null || service==null || !service.IsShuffle || !OwnPlaylist(service)) return original;
            var display=service.CurrentPlayList;
            var source=AccessTools.Field(typeof(MusicService),"shuffleList").GetValue(service) as List<GameAudioInfo>;
            if(source==null) return original;
            if(owner.nativePlaybackOrder==null || !ReferenceEquals(source,owner.nativeQueueSource)
                || owner.nativeQueueDisplay==null || !display.SequenceEqual(owner.nativeQueueDisplay))
            {
                owner.nativeQueueSource=source;owner.nativeQueueDisplay=display.ToArray();
                owner.nativePlaybackOrder=new ObservableList<GameAudioInfo>();
                owner.nativePlaybackOrder.AddRange(ShufflePolicy.VisibleOrder(source,display));
            }
            return owner.nativePlaybackOrder;
        }
        static int NativeFailureOffset(MusicService service,GameAudioInfo failed)
        {
            var queue=NativeShuffleRead(service.CurrentPlayList);
            return ShufflePolicy.OffsetAfter(queue,service.PlayingMusic,failed);
        }
    }
}
