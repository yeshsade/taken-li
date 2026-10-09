using System;
using System.Runtime.InteropServices;

namespace TakenLi.Windows;

internal static class Native
{
    internal const int WmHotkey = 0x0312;
    internal const int WmInputLanguageChangeRequest = 0x0050;
    internal const int KeyDown = 0x0100;
    internal const int SysKeyDown = 0x0104;
    internal const uint Injected = 0x10;

    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] internal static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? module);
    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        internal uint Key, Scan, Flags, Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort Key, Scan;
        internal uint Flags, Time;
        internal UIntPtr ExtraInfo;
    }

    internal static void SendKeys(params (int Key, bool Up)[] keys)
    {
        var inputs = new Input[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            inputs[i].Type = 1;
            // Home/End and the other navigation keys belong to the extended
            // keyboard block, not the numeric keypad. Word and shell editors
            // can distinguish these events even when their virtual key matches.
            var extended = keys[i].Key is >= 0x21 and <= 0x28 or 0x2D or 0x2E;
            inputs[i].Data.Keyboard = new KeyboardInput { Key = (ushort)keys[i].Key, Flags = (keys[i].Up ? 2u : 0u) | (extended ? 1u : 0u) };
        }
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            // Best-effort release if Windows accepted only part of a key chord.
            for (var i = 0; i < inputs.Length; i++) inputs[i].Data.Keyboard.Flags = (inputs[i].Data.Keyboard.Flags & 1u) | 2u;
            if (sent > 0) SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            throw new InvalidOperationException("input");
        }
    }

    internal static void Chord(int key, bool shift = false)
    {
        if (shift) SendKeys((16, false), (key, false), (key, true), (16, true));
        else SendKeys((17, false), (key, false), (key, true), (17, true));
    }

    internal static ushort Language(IntPtr window)
    {
        var thread = GetWindowThreadProcessId(window, out _);
        return (ushort)(GetKeyboardLayout(thread).ToInt64() & 0xffff);
    }

    internal static bool IsEnglish(IntPtr window) => (Language(window) & 0x3ff) == 9;

    internal static bool CapsLockOn => (GetKeyState(0x14) & 1) != 0;

    internal static void DisableCapsLockIfOn()
    {
        // Caps Lock is a toggle: sending it while already off would enable it.
        if (CapsLockOn) SendKeys((0x14, false), (0x14, true));
    }

    internal static bool SwitchExistingLayout(IntPtr window, bool hebrew, bool disableCapsLockOnHebrew)
    {
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0) return false;
        var layouts = new IntPtr[count];
        var actual = GetKeyboardLayoutList(count, layouts);
        for (var i = 0; i < actual; i++)
        {
            var language = (ushort)(layouts[i].ToInt64() & 0xffff);
            if (hebrew ? language == 0x040d : (language & 0x3ff) == 9)
            {
                if (!PostMessage(window, WmInputLanguageChangeRequest, IntPtr.Zero, layouts[i])) return false;
                if (hebrew && disableCapsLockOnHebrew && GetForegroundWindow() == window) DisableCapsLockIfOn();
                return true;
            }
        }
        return false;
    }
}
