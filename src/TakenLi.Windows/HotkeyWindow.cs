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

internal sealed class TypingMonitor
{
    private uint? _lastInputTick;
    internal event Action? Typed;

    internal void Poll()
    {
        // Windows can silently remove a low-level keyboard hook after a timeout.
        // Poll the session's activity timestamp instead: no key or text is read,
        // and long UI/clipboard operations cannot disconnect this monitor.
        // Mouse activity also postpones the return, conservatively.
        if (!Native.TryGetLastInputTick(out var tick))
        {
            // If activity cannot be checked, never switch under the user's hands.
            Typed?.Invoke();
            return;
        }
        if (_lastInputTick == tick) return;
        _lastInputTick = tick;
        Typed?.Invoke();
    }
}
