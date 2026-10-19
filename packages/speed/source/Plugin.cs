// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using Bulbul;
using HarmonyLib;
using KanKikuchi.AudioManager;
using UnityEngine;
namespace ChillPlaybackSpeed
{
    [BepInPlugin("local.chill.playbackspeed", "播放倍速", "1.0.0")]
    [BepInProcess("Chill With You.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        static Plugin instance;
        MusicService service;
        MusicManager manager;
        Harmony harmony;
        SpeedRuntimeHost host;
        readonly SpeedController controller = new SpeedController();
        ConfigEntry<float> rate;
        ConfigEntry<bool> speedEnabled;
        bool visible, wasDown, failed;
        GUIStyle windowStyle, textStyle, captionStyle, buttonStyle, selectedStyle;
        readonly List<Texture2D> surfaces = new List<Texture2D>();
        Rect rect = new Rect(490, 270, 620, 310);
        string hint = "播放六个平台的音频后生效。";
        AudioSource reportedSource;
        float reportedPitch;
        void Awake()
        {
            instance = this;
            gameObject.hideFlags = HideFlags.HideAndDontSave; DontDestroyOnLoad(gameObject);
            rate = Config.Bind("Speed", "Rate", 1f, new ConfigDescription("六平台音频倍速，0.5～2，步长 0.05；会随速度改变音调。", new AcceptableValueRange<float>(0.5f,2f)));
            speedEnabled = Config.Bind("Speed", "Enabled", true, "启用六平台音频倍速；不会改变游戏、角色语音或环境音速度。");
            rate.Value = SpeedController.Rate(rate.Value);
            var runtime = new GameObject("ChillPlaybackSpeed.Runtime"); runtime.hideFlags = HideFlags.HideAndDontSave; DontDestroyOnLoad(runtime);
            host = runtime.AddComponent<SpeedRuntimeHost>(); host.Owner = this;
            harmony = new Harmony("local.chill.playbackspeed");
            try
            {
                foreach (ConstructorInfo ctor in typeof(MusicService).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(Plugin), "Capture"));
                Logger.LogInfo("六平台播放倍速已接入；F5 设置，0.5×～2×，默认 1×。倍速会改变音调。");
            }
            catch(Exception e) { failed = true; harmony.UnpatchSelf(); Logger.LogError("倍速未启用：" + e.GetType().Name); }
        }
        static void Capture(MusicService __instance) { if (instance != null) instance.service = __instance; }
        internal void Tick()
        {
            bool down = SpeedRuntimeHost.F5Down();
            if (down && !wasDown) visible = !visible;
            wasDown = down;
        }
        internal void ApplySpeed()
        {
            if (failed) return;
            try
            {
                var info = service == null ? null : service.PlayingMusic;
                AudioSource source = null;
                if (info != null && SpeedController.Supported(info.UUID) && info.AudioClip != null)
                {
                    if (manager == null) { var all = Resources.FindObjectsOfTypeAll<MusicManager>(); if (all.Length > 0) manager = all[0]; }
                    var player = manager == null ? null : manager.GetPlayer(info.AudioClip);
                    if (player != null && player.AudioSource != null && player.AudioSource.clip == info.AudioClip) source = player.AudioSource;
                }
                controller.Apply(source, info == null ? null : info.UUID, rate.Value, speedEnabled.Value);
                if (source != null && speedEnabled.Value && (source != reportedSource || Math.Abs(source.pitch-reportedPitch)>0.001f))
                { reportedSource=source; reportedPitch=source.pitch; Logger.LogInfo("音乐音源实际倍速：" + source.pitch.ToString("0.##",CultureInfo.InvariantCulture) + "×"); }
                hint = source == null ? "播放六个平台的音频后生效。" : speedEnabled.Value ? "当前音频 · " + DisplayRate() : "倍速已关闭，按原速度播放。";
            }
            catch(Exception e) { controller.Release(); failed = true; Logger.LogWarning("倍速暂不可用：" + e.GetType().Name); }
        }
        string DisplayRate() { return SpeedController.Rate(rate.Value).ToString("0.##", CultureInfo.InvariantCulture) + "×"; }
        Texture2D Surface(Color color) { var t = GameTheme.Surface(color,true); surfaces.Add(t); return t; }
        void EnsureUI()
        {
            if (textStyle != null) return;
            textStyle = new GUIStyle(GUI.skin.label); textStyle.fontSize = 21; textStyle.normal.textColor = GameTheme.Ink; textStyle.wordWrap = true;
            captionStyle = new GUIStyle(textStyle); captionStyle.fontSize = 15; captionStyle.normal.textColor = GameTheme.Muted;
            windowStyle = new GUIStyle(GUI.skin.window); windowStyle.normal.background = Surface(GameTheme.Window); windowStyle.onNormal.background = windowStyle.normal.background;
            buttonStyle = new GUIStyle(GUI.skin.button); buttonStyle.fontSize = 17; buttonStyle.normal.textColor = GameTheme.Ink;
            buttonStyle.normal.background = Surface(GameTheme.Button); buttonStyle.hover.background = Surface(GameTheme.Hover); buttonStyle.active.background = Surface(GameTheme.Selected);
            selectedStyle = new GUIStyle(buttonStyle); selectedStyle.normal.background = buttonStyle.active.background;
        }
        internal void Draw()
        {
            if (!visible) return;
            Matrix4x4 oldMatrix = GUI.matrix; int oldDepth = GUI.depth;
            try
            {
                float scale = Math.Max(0.35f, Math.Min(Screen.width/1600f,Screen.height/900f)); GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1)); GUI.depth = -100;
                EnsureUI(); rect.x = Mathf.Clamp(rect.x,0,Mathf.Max(0,Screen.width/scale-rect.width)); rect.y = Mathf.Clamp(rect.y,0,Mathf.Max(0,Screen.height/scale-rect.height));
                rect = GUI.Window(186050,rect,DrawWindow,"",windowStyle);
            }
            finally { GUI.matrix = oldMatrix; GUI.depth = oldDepth; }
        }
        void SetRate(float value) { rate.Value = SpeedController.Rate(value); Config.Save(); ApplySpeed(); Logger.LogInfo("播放倍速已保存：" + DisplayRate()); }
        void DrawWindow(int id)
        {
            GUI.Label(new Rect(24,18,440,35),"播放倍速  ·  " + DisplayRate(),textStyle);
            if (GUI.Button(new Rect(520,16,76,32),"收起 F5",buttonStyle)) visible = false;
            GUI.Label(new Rect(24,61,570,28),hint,captionStyle);
            float[] presets = {0.5f,0.75f,1f,1.25f,1.5f,1.75f,2f};
            for(int i=0;i<presets.Length;i++)
                if(GUI.Button(new Rect(24+i*82,103,76,42),presets[i].ToString("0.##",CultureInfo.InvariantCulture)+"×",Math.Abs(rate.Value-presets[i])<0.001f?selectedStyle:buttonStyle)) SetRate(presets[i]);
            if(GUI.Button(new Rect(24,163,108,38),"− 0.05",buttonStyle)) SetRate(rate.Value-0.05f);
            if(GUI.Button(new Rect(145,163,108,38),"+ 0.05",buttonStyle)) SetRate(rate.Value+0.05f);
            if(GUI.Button(new Rect(267,163,142,38),"恢复 1×",buttonStyle)) SetRate(1);
            if(GUI.Button(new Rect(423,163,173,38),speedEnabled.Value?"倍速：开":"倍速：关",buttonStyle)) { speedEnabled.Value=!speedEnabled.Value; Config.Save(); ApplySpeed(); }
            GUI.Label(new Rect(24,222,572,64),"切歌和重启保留选择。仅作用于六平台音频。\n当前使用原生变速，音调也会变化。歌词跟随实际播放位置。",captionStyle);
            GUI.DragWindow(new Rect(0,0,510,55));
        }
        void OnDestroy()
        {
            controller.Release(); if(harmony!=null) harmony.UnpatchSelf();
            if(instance==this) instance=null; if(host!=null) Destroy(host.gameObject);
            foreach(var texture in surfaces) if(texture!=null) Destroy(texture);
        }
    }
    public sealed class SpeedRuntimeHost : MonoBehaviour
    {
        internal Plugin Owner;
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint id);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        internal static bool F5Down()
        {
            uint id; GetWindowThreadProcessId(GetForegroundWindow(),out id); if(id!=GetCurrentProcessId()) return false;
            foreach(int modifier in new[]{0x10,0x11,0x12,0x5B,0x5C}) if((GetAsyncKeyState(modifier)&0x8000)!=0) return false;
            try { if(Input.GetKey(KeyCode.F5) || Input.GetKeyDown(KeyCode.F5)) return true; }catch(InvalidOperationException){}
            return (GetAsyncKeyState(0x74)&0x8000)!=0;
        }
        void Update() { if(Owner!=null) Owner.Tick(); }
        void LateUpdate() { if(Owner!=null) Owner.ApplySpeed(); }
        void OnGUI() { if(Owner!=null) Owner.Draw(); }
    }
}
