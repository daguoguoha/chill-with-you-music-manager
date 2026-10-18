// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChillSaltPlayerPlaylist
{
    public sealed class RuntimeHost : MonoBehaviour
    {
        internal Plugin Owner;
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        internal static bool IsF6DownInGame()
        {
            uint processId;
            GetWindowThreadProcessId(GetForegroundWindow(), out processId);
            return processId == GetCurrentProcessId() && (GetAsyncKeyState(0x75) & 0x8000) != 0;
        }
        internal static bool IsVDownInGame()
        {
            uint processId;
            GetWindowThreadProcessId(GetForegroundWindow(), out processId);
            if (processId != GetCurrentProcessId()) return false;
            try { if (Input.GetKey(KeyCode.V) || Input.GetKeyDown(KeyCode.V)) return true; } catch (InvalidOperationException) { }
            return (GetAsyncKeyState(0x56) & 0x8000) != 0;
        }
        internal static bool HasLyricShortcutModifier()
        {
            return ((GetAsyncKeyState(0x10) | GetAsyncKeyState(0x11) | GetAsyncKeyState(0x12) | GetAsyncKeyState(0x5B) | GetAsyncKeyState(0x5C)) & 0x8000) != 0;
        }
        internal static bool IsLeftMouseDownInGame()
        {
            uint processId;
            GetWindowThreadProcessId(GetForegroundWindow(), out processId);
            return processId == GetCurrentProcessId() && (GetAsyncKeyState(1) & 0x8000) != 0;
        }
        void Update() { if (Owner != null) Owner.Tick(); }
        void OnGUI() { if (Owner != null) Owner.RenderPanel(); }
    }
}
