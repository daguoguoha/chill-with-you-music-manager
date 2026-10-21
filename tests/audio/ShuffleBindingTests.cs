// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Bulbul;
using PLATFORM_NAMESPACE;
namespace Bulbul { public class GameAudioInfo {} }
namespace NestopiSystem
{
    public static class IEnumerableExtensions
    {
        public static void Move<T>(IList<T> list, int from, int to) { throw new Exception("unguarded"); }
    }
}
public static class ShuffleBindingTests
{
    public static int Main()
    {
        try { Run(); return 0; }
        catch(Exception error) { Console.WriteLine("FAIL native binding: " + error.GetType().Name); return 1; }
    }
    static void Run()
    {
        var type=typeof(Plugin);
        var transpiler=type.GetMethod("NativeShuffleBounds", BindingFlags.Static|BindingFlags.NonPublic);
        var move=typeof(NestopiSystem.IEnumerableExtensions).GetMethod("Move").MakeGenericMethod(typeof(GameAudioInfo));
        var original=new CodeInstruction(OpCodes.Call,move);
        var list=new List<CodeInstruction>{new CodeInstruction(OpCodes.Nop),original,new CodeInstruction(OpCodes.Ret)};
        var once=((IEnumerable<CodeInstruction>)transpiler.Invoke(null,new object[]{list})).ToList();
        var twice=((IEnumerable<CodeInstruction>)transpiler.Invoke(null,new object[]{once})).ToList();
        var replacement=(MethodInfo)once[1].operand;
        if(replacement.Name!="NativeShuffleMove" || twice[1].operand!=once[1].operand || once.Count!=3) throw new Exception("binding/idempotency");
        var a=new GameAudioInfo();var b=new GameAudioInfo();var c=new GameAudioInfo();
        var queue=new List<GameAudioInfo>{a,b,c};
        replacement.Invoke(null,new object[]{queue,-1,0});
        if(!queue.SequenceEqual(new[]{a,b,c}))throw new Exception("absent old track");
        replacement.Invoke(null,new object[]{queue,2,0});
        if(!queue.SequenceEqual(new[]{c,a,b}))throw new Exception("preserve playing first");
        replacement.Invoke(null,new object[]{new List<GameAudioInfo>(),-1,0});
        Console.WriteLine("PASS native shuffle binding, repeated patch, absent track, current track and empty queue");
    }
}
