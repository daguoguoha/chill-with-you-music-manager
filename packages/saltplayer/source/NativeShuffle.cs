// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Bulbul;
using HarmonyLib;
namespace ChillSaltPlayerPlaylist
{
    public sealed partial class Plugin
    {
        // The native button owns IsShuffle, persistence, notifications and queue filtering.
        // Guard its Move call when the previous track was removed by a playlist switch.
        static IEnumerable<CodeInstruction> NativeShuffleBounds(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo method = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && method != null && method.Name == "Move"
                    && method.DeclaringType.FullName == "NestopiSystem.IEnumerableExtensions"
                    && method.IsGenericMethod && method.GetParameters()[0].ParameterType.IsGenericType
                    && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IList<>))
                    instruction.operand = AccessTools.Method(typeof(Plugin), "NativeShuffleMove");
                yield return instruction;
            }
        }
        static void NativeShuffleMove(IList<GameAudioInfo> list, int from, int to)
        {
            if (from < 0 || from >= list.Count || to < 0 || to >= list.Count || from == to) return;
            GameAudioInfo item = list[from]; list.RemoveAt(from); list.Insert(to, item);
        }
    }
}
