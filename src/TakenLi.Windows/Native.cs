using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TakenLi.Windows;

internal static class Native
{
    internal const int WmHotkey = 0x0312;
    internal const int WmInputLanguageChangeRequest = 0x0050;

    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal uint Size, Flags;
        internal IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        internal int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] internal static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        internal uint Size, Tick;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

    internal static bool IsDesktopOrTaskbar(IntPtr window)
    {
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return IsDesktopOrTaskbarClass(name.ToString());
    }

    internal static bool IsDesktopOrTaskbarClass(string name) => name is
        "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW" or
        "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" or "XamlExplorerHostIslandWindow";

    internal static bool TryGetLastInputTick(out uint tick)
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        var success = GetLastInputInfo(ref info);
        tick = info.Tick;
        return success;
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
        return (ushort)(Layout(window).ToInt64() & 0xffff);
    }

    internal static IntPtr Layout(IntPtr window)
    {
        var target = FocusWithin(window);
        var thread = GetWindowThreadProcessId(target, out _);
        return thread == 0 ? IntPtr.Zero : GetKeyboardLayout(thread);
    }

    private static IntPtr FocusWithin(IntPtr window)
    {
        var thread = GetWindowThreadProcessId(window, out _);
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        return thread != 0 && GetGUIThreadInfo(thread, ref info) && info.Active == window && info.Focus != IntPtr.Zero
            ? info.Focus : window;
    }

    internal static IntPtr ExistingLayout(bool hebrew)
    {
        var layouts = new IntPtr[Math.Max(0, GetKeyboardLayoutList(0, null))];
        var count = GetKeyboardLayoutList(layouts.Length, layouts);
        for (var i = 0; i < count; i++)
        {
            var language = (ushort)(layouts[i].ToInt64() & 0xffff);
            if (hebrew ? language == 0x040d : (language & 0x3ff) == 9) return layouts[i];
        }
        return IntPtr.Zero;
    }

    internal static bool RestoreExistingLayout(IntPtr window, IntPtr layout, bool disableCapsLock)
    {
        if (GetForegroundWindow() != window) return false;
        var layouts = new IntPtr[Math.Max(0, GetKeyboardLayoutList(0, null))];
        var count = GetKeyboardLayoutList(layouts.Length, layouts);
        if (Array.IndexOf(layouts, layout, 0, Math.Max(0, count)) < 0) return false;
        if (!PostLayout(window, layout)) return false;
        if ((layout.ToInt64() & 0xffff) == 0x040d && disableCapsLock) DisableCapsLockIfOn();
        return true;
    }

    private static bool PostLayout(IntPtr window, IntPtr layout)
    {
        var target = FocusWithin(window);
        return GetForegroundWindow() == window && PostMessage(target, WmInputLanguageChangeRequest, IntPtr.Zero, layout);
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
        var layout = ExistingLayout(hebrew);
        return layout != IntPtr.Zero && RestoreExistingLayout(window, layout, disableCapsLockOnHebrew);
    }
}
