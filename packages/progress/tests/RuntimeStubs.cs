// SPDX-License-Identifier: GPL-3.0-or-later
// Deterministic adapter tests only. These stand-ins do not represent a running Unity game.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine
{
    public class Object { public static void DontDestroyOnLoad(Object o) {} public static void Destroy(Object o) {} }
    public enum HideFlags { HideAndDontSave }
    public class GameObject : Object { public HideFlags hideFlags; public GameObject(string name) {} public T AddComponent<T>() where T:new() {return new T();} }
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject("test"); }
    public class AudioClip : Object { public float length; }
    public class AudioSource : Object { public AudioClip clip; public float time; public bool isPlaying; }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Resources { public static object Items; public static T[] FindObjectsOfTypeAll<T>() {return Items == null ? new T[0] : new[]{(T)Items};} }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; public ConfigEntry(T v) {Value=v;} }
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string s,string k,T v,string d){return new ConfigEntry<T>(v);} }
}
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string a,string b,string c){} }
    public class BepInProcess : Attribute { public BepInProcess(string a){} }
    public class TestLog { public void LogInfo(object s){} public void LogWarning(object s){} public void LogError(object s){} }
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Configuration.ConfigFile Config=new Configuration.ConfigFile(); public TestLog Logger=new TestLog(); }
    public static class Paths { public static string ConfigPath; }
}
namespace HarmonyLib
{
    public class HarmonyMethod { public HarmonyMethod(Type t,string n){} }
    public class Harmony { public Harmony(string id){} public void Patch(MethodBase m,HarmonyMethod prefix=null,HarmonyMethod postfix=null){} public void UnpatchSelf(){} }
    public static class AccessTools
    {
        public static MethodInfo Method(Type t,string n){return t.GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);}
        public static MethodInfo PropertySetter(Type t,string n){return t.GetProperty(n).GetSetMethod(true);}
    }
}
namespace Bulbul { public class GameAudioInfo { public string UUID; public UnityEngine.AudioClip AudioClip; } }
namespace KanKikuchi.AudioManager
{
    public class AudioPlayer { public UnityEngine.AudioSource AudioSource; }
    public class MusicManager : UnityEngine.Object
    {
        public AudioPlayer Player;
        public AudioPlayer GetPlayer(UnityEngine.AudioClip clip) {return Player != null && Player.AudioSource.clip == clip ? Player : null;}
    }
}
public class MusicService { public Bulbul.GameAudioInfo PlayingMusic {get;set;} }
