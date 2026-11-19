using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TakenLi.Windows;

// Raw input is used only to confirm a layout choice. Idle timing uses the
// independent session activity timestamp even if raw input is unavailable.
internal sealed class LayoutInputMonitor : NativeWindow, IDisposable
{
    private const uint Foreground = 3;
    private const uint ReadInput = 0x10000003;
    private const int WmInput = 0x00ff;
    private readonly ForegroundCallback _onForeground;
    private readonly IntPtr _foregroundHook;
    internal bool KeyboardRegistered { get; }
    internal event Action<IntPtr>? ForegroundChanged;
    internal event Action? TextKeyPressed;

    internal LayoutInputMonitor()
    {
        CreateHandle(new CreateParams { Caption = "TakenLi layout input", Parent = new IntPtr(-3) });
        KeyboardRegistered = RegisterKeyboard(0x100, Handle); // RIDEV_INPUTSINK
        _onForeground = (_, _, window, _, _, _, _) => ForegroundChanged?.Invoke(window);
        _foregroundHook = SetWinEventHook(Foreground, Foreground, IntPtr.Zero, _onForeground, 0, 0, 0);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmInput)
        {
            uint size = (uint)Marshal.SizeOf<KeyboardEvent>();
            var read = GetRawInputData(message.LParam, ReadInput, out var input, ref size, (uint)Marshal.SizeOf<InputHeader>());
            if (read == size && input.Header.Type == 1 && input.Header.Device != IntPtr.Zero &&
                (input.Flags & 1) == 0 && IsTextKey(input.VirtualKey) &&
                !Down(0x11) && !Down(0x12) && !Down(0x5b) && !Down(0x5c))
                TextKeyPressed?.Invoke();
        }
        base.WndProc(ref message);
    }

    internal static bool IsTextKey(ushort key) =>
        key is 0x20 or >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5a or
            >= 0x60 and <= 0x6f or >= 0xba and <= 0xc0 or >= 0xdb and <= 0xde or 0xe2;

    private static bool Down(int key) => Native.GetAsyncKeyState(key) < 0;

    private static bool RegisterKeyboard(uint flags, IntPtr target)
    {
        var device = new InputDevice { UsagePage = 1, Usage = 6, Flags = flags, Target = target };
        return RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<InputDevice>());
    }

    public void Dispose()
    {
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        if (KeyboardRegistered) RegisterKeyboard(1, IntPtr.Zero); // RIDEV_REMOVE
        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputDevice
    {
        internal ushort UsagePage, Usage;
        internal uint Flags;
        internal IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputHeader
    {
        internal uint Type, Size;
        internal IntPtr Device, Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardEvent
    {
        internal InputHeader Header;
        internal ushort MakeCode, Flags, Reserved, VirtualKey;
        internal uint Message, ExtraInformation;
    }

    private delegate void ForegroundCallback(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(ref InputDevice devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr input, uint command, out KeyboardEvent data, ref uint size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, ForegroundCallback callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
