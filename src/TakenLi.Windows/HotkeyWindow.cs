using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private AppSettings? _registered;
    private readonly List<int> _ids = new();
    internal event Action<TextAction>? ActionRequested;

    internal HotkeyWindow() => CreateHandle(new CreateParams { Caption = "TakenLi hotkeys", Parent = new IntPtr(-3) });

    internal void Apply(AppSettings settings)
    {
        settings.Validate();
        var previous = _registered;
        Clear();
        try { Register(settings); _registered = settings.Copy(); }
        catch
        {
            Clear();
            if (previous is not null) Register(previous);
            _registered = previous;
            throw;
        }
    }

    private void Register(AppSettings settings)
    {
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var key = settings.KeyFor(action);
            var id = (int)action + 1;
            if (!Native.RegisterHotKey(Handle, id, (uint)key.Modifiers | 0x4000, (uint)key.Key))
                throw new InvalidOperationException($"hotkey:{key.Display}:{Marshal.GetLastWin32Error()}");
            _ids.Add(id);
        }
    }

    internal void Clear()
    {
        foreach (var id in _ids) Native.UnregisterHotKey(Handle, id);
        _ids.Clear();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == Native.WmHotkey) ActionRequested?.Invoke((TextAction)(message.WParam.ToInt32() - 1));
        base.WndProc(ref message);
    }

    public void Dispose() { Clear(); DestroyHandle(); }
}

internal sealed class TypingMonitor : IDisposable
{
    private readonly Native.HookProc _callback;
    private readonly IntPtr _hook;
    internal event Action? Typed;

    internal TypingMonitor()
    {
        _callback = Callback;
        _hook = Native.SetWindowsHookEx(13, _callback, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new InvalidOperationException("hook");
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt64() is Native.KeyDown or Native.SysKeyDown)
        {
            var data = Marshal.PtrToStructure<Native.KeyboardHookData>(lParam);
            // Track activity only: no text, key sequence or history is retained.
            if ((data.Flags & Native.Injected) == 0) Typed?.Invoke();
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose() => Native.UnhookWindowsHookEx(_hook);
}
