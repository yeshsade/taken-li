using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class TrayContext : ApplicationContext
{
    private AppSettings _settings;
    private readonly HotkeyWindow _hotkeys = new();
    private readonly TextEditor _editor = new();
    private readonly IdleReturnPolicy _idle = new();
    private readonly ApplicationLayouts _layouts;
    private readonly LayoutInputMonitor _layoutInput;
    private bool _layoutSaveWarning;
    private IntPtr _observedWindow;
    private readonly TypingMonitor _typing;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly Icon _icon;
    private bool _paused;
    private bool _firstTick = true;
    private bool _layoutWarning;
    private bool _closing;
    private Exception? _initialHotkeyError;
    private SettingsForm? _settingsForm;
    private WelcomeForm? _welcome;
    private IntPtr _lastExternalWindow;
    private IntPtr _lastTimerWindow;
    private ApplicationTarget? _lastTimerApplication;
    private uint _lastProcess;
    private string? _lastProcessName;

    internal TrayContext(AppSettings settings)
    {
        _settings = settings;
        _layouts = new ApplicationLayouts(LoadRememberedLayouts(), SaveRememberedLayouts);
        _editor.LayoutRequested += (window, layout) =>
        {
            if (ApplicationName(window) is string application)
                _layouts.ExplicitChange(application, window.ToInt64(), layout.ToInt64(), Environment.TickCount64);
        };
        _typing = new TypingMonitor();
        _typing.Typed += () => _idle.Typed(Environment.TickCount64);
        _icon = MakeIcon();
        _tray = new NotifyIcon { Icon = _icon, Visible = true, Text = "TakenLi" };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _layoutInput = new LayoutInputMonitor();
        _layoutInput.ForegroundChanged += window =>
        {
            if (window == Native.GetForegroundWindow()) ObserveApplication(window, Environment.TickCount64);
        };
        _layoutInput.TextKeyPressed += ConfirmTyping;
        _hotkeys.ActionRequested += action => RunAction(action, Native.GetForegroundWindow());
        try { _hotkeys.Apply(_settings); }
        catch (Exception error) { _initialHotkeyError = error; }
        RefreshMenu();
        _timer.Tick += Tick;
        _timer.Start();
    }

    private static Icon MakeIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Ui.Accent);
            graphics.FillRectangle(background, 2, 2, 28, 28);
            using var font = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString("א", font, Brushes.White, new RectangleF(2, 0, 28, 30), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    private void RefreshMenu()
    {
        var ui = new Ui(_settings.Language);
        var menu = new ContextMenuStrip { RightToLeft = ui.Direction };
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var item = new ToolStripMenuItem(ui.ActionName(action)) { ShortcutKeyDisplayString = _settings.KeyFor(action).Display, Enabled = !_paused };
            item.Click += (_, _) => RunAction(action, _lastExternalWindow);
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        ApplicationTimerMenu.AddTo(menu, ui, () => _lastTimerApplication, () => _settings,
            SaveApplicationTimer, Report, () => !_closing && _settingsForm is null);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ui.T("הגדרות", "Settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(ui.T("הסבר", "Help"), null, (_, _) => ShowWelcome());
        menu.Items.Add(ui.T(_paused ? "חידוש הפעולה" : "השהיה", _paused ? "Resume" : "Pause"), null, (_, _) => TogglePause());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ui.T("יציאה", "Exit"), null, async (_, _) =>
        {
            _closing = true;
            _timer.Stop();
            while (_editor.Busy) await System.Threading.Tasks.Task.Delay(20);
            ExitThread();
        });
        var previous = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        previous?.Dispose();
        _tray.Text = _paused ? ui.T("TakenLi — מושהה", "TakenLi — Paused") : "TakenLi";
    }

    private async void RunAction(TextAction action, IntPtr target)
    {
        if (_closing || _paused || _editor.Busy || _settingsForm is not null || _welcome is not null || !External(target)) return;
        try { await _editor.EditAsync(target, action, _settings); }
        catch (Exception error) { Report(error); }
        finally { _idle.Reset(Environment.TickCount64); }
    }

    private static bool External(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(window, out var process);
        return process != 0 && process != Environment.ProcessId;
    }

    private void Tick(object? sender, EventArgs args)
    {
        var window = Native.GetForegroundWindow();
        if (External(window))
        {
            _lastExternalWindow = window;
            if (window != _lastTimerWindow && !Native.IsDesktopOrTaskbar(window) && !(_tray.ContextMenuStrip?.Visible ?? false))
            {
                _lastTimerWindow = window;
                var executable = ApplicationName(window);
                _lastTimerApplication = executable is null ? null : new ApplicationTarget(executable, executable[..^4]);
                if (_lastTimerApplication is not null)
                {
                    try
                    {
                        Native.GetWindowThreadProcessId(window, out var id);
                        using var process = Process.GetProcessById((int)id);
                        var version = process.MainModule?.FileVersionInfo;
                        var product = string.IsNullOrWhiteSpace(version?.FileDescription) ? version?.ProductName : version.FileDescription;
                        if (!string.IsNullOrWhiteSpace(product)) _lastTimerApplication = new ApplicationTarget(executable!, product);
                    }
                    catch (Exception) { /* The executable name is a readable fallback. */ }
                }
            }
        }
        if (_firstTick)
        {
            _firstTick = false;
            if (_initialHotkeyError is not null) { Report(_initialHotkeyError); ShowSettings(); }
            else if (_settings.ShowWelcome) ShowWelcome();
            return;
        }
        var now = Environment.TickCount64;
        _typing.Poll();
        if (ObserveApplication(window, now)) return;
        if (_paused || _editor.Busy || !_settings.AutoReturnEnabled || _observedWindow != window || !External(window) || IsExcluded(window))
        {
            _idle.Reset(now);
            return;
        }
        if (_idle.ShouldReturn(window.ToInt64(), Native.IsEnglish(window), now, _settings.AutoReturnSeconds))
        {
            try
            {
                var hebrew = Native.ExistingLayout(true);
                if (Native.RestoreExistingLayout(window, hebrew, false))
                {
                    _layouts.AutomaticReturn(hebrew.ToInt64(), now);
                    if (_settings.DisableCapsLockOnHebrew && Native.GetForegroundWindow() == window)
                        Native.DisableCapsLockIfOn();
                }
                else if (!_layoutWarning)
                {
                    _layoutWarning = true;
                    Report(new InvalidOperationException("layout"));
                }
            }
            catch (InvalidOperationException error)
            {
                // Input can be blocked by Windows. A timer callback must not
                // terminate the app when the Caps Lock key cannot be sent.
                if (!_layoutWarning) { _layoutWarning = true; Report(error); }
            }
        }
    }

    private bool ObserveApplication(IntPtr window, long now)
    {
        if (_closing || _paused || _editor.Busy || window == IntPtr.Zero || Native.IsDesktopOrTaskbar(window)) return false;
        var application = External(window) ? ApplicationName(window) : null;
        var restore = _layouts.Observe(application, window.ToInt64(), Native.Layout(window).ToInt64(), now);
        if (_observedWindow != window || restore is not null) _idle.Reset(now);
        _observedWindow = window;
        if (restore is not long layout) return false;
        try
        {
            if (!Native.RestoreExistingLayout(window, new IntPtr(layout), _settings.DisableCapsLockOnHebrew))
                _layouts.RequestRejected();
        }
        catch (InvalidOperationException error) { Report(error); }
        return true;
    }

    private void ConfirmTyping()
    {
        if (_closing || _paused || _editor.Busy) return;
        var window = Native.GetForegroundWindow();
        if (window != _observedWindow || ApplicationName(window) is not string application) return;
        _layouts.Typing(application, window.ToInt64(), Native.Layout(window).ToInt64(), Environment.TickCount64);
    }

    private static System.Collections.Generic.Dictionary<string, long>? LoadRememberedLayouts()
    {
        try { return SettingsStore.LoadLayouts(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void SaveRememberedLayouts()
    {
        try
        {
            SettingsStore.SaveLayouts(_layouts.Remembered);
            _layoutSaveWarning = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (!_layoutSaveWarning) Report(error);
            _layoutSaveWarning = true;
        }
    }

    private string? ApplicationName(IntPtr window)
    {
        Native.GetWindowThreadProcessId(window, out var processId);
        if (_lastProcess != processId)
        {
            _lastProcess = processId;
            try { using var process = Process.GetProcessById((int)processId); _lastProcessName = process.ProcessName + ".exe"; }
            catch (Exception) { _lastProcessName = null; }
        }
        return _lastProcessName;
    }

    private bool IsExcluded(IntPtr window)
    {
        var name = ApplicationName(window);
        return _settings.ExcludedApplications.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    }

    private void TogglePause()
    {
        try
        {
            if (_paused) _hotkeys.Apply(_settings); else _hotkeys.Clear();
            _paused = !_paused;
            _idle.Reset(Environment.TickCount64);
            RefreshMenu();
        }
        catch (Exception error) { Report(error); }
    }

    private void SaveApplicationTimer(AppSettings next)
    {
        // Only the exclusion changes: do not touch Startup or re-register keys.
        SettingsStore.Save(next);
        _settings = next;
        _idle.Reset(Environment.TickCount64);
    }

    private void SaveSettings(AppSettings next)
    {
        var previous = _settings;
        next.Validate();
        var startupChanged = false;
        try
        {
            _hotkeys.Apply(next); // Detect conflicts before writing settings.
            SettingsStore.SetStartup(next.StartWithWindows);
            startupChanged = true;
            SettingsStore.Save(next);
        }
        catch
        {
            if (startupChanged)
            {
                try { SettingsStore.SetStartup(previous.StartWithWindows); } catch (Exception) { }
            }
            try { _hotkeys.Apply(previous); } catch (Exception) { }
            throw;
        }
        finally { if (_paused || _settingsForm is not null) _hotkeys.Clear(); }
        _settings = next;
        _layoutWarning = false;
        _idle.Reset(Environment.TickCount64);
        RefreshMenu();
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null) { _settingsForm.Activate(); return; }
        // Release global shortcuts while capturing keys in the settings dialog.
        _hotkeys.Clear();
        _settingsForm = new SettingsForm(_settings, SaveSettings, ShowWelcome);
        _settingsForm.Icon = _icon;
        _settingsForm.FormClosed += (_, _) =>
        {
            _settingsForm = null;
            if (!_paused)
            {
                try { _hotkeys.Apply(_settings); }
                catch (Exception error) { Report(error); }
            }
        };
        _settingsForm.Show();
    }

    private void ShowWelcome()
    {
        if (_welcome is not null) { _welcome.Activate(); return; }
        _welcome = new WelcomeForm(_settings);
        _welcome.Icon = _icon;
        _welcome.FormClosed += (_, _) =>
        {
            var form = _welcome!;
            _welcome = null;
            if (_settings.ShowWelcome == form.DoNotShowAgain)
            {
                var next = _settings.Copy();
                next.ShowWelcome = !form.DoNotShowAgain;
                try { SaveSettings(next); }
                catch (Exception error) { Report(error); }
            }
            if (form.OpenSettings) ShowSettings();
        };
        _welcome.Show();
    }

    private void Report(Exception error)
    {
        var ui = new Ui(_settings.Language);
        _tray.ShowBalloonTip(7000, ui.T("לא ניתן להשלים את הפעולה", "Action could not be completed"), ui.Error(error), ToolTipIcon.Warning);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _settingsForm?.Dispose();
        _welcome?.Dispose();
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _icon.Dispose();
        _layoutInput.Dispose();
        _hotkeys.Dispose();
        _timer.Dispose();
        base.ExitThreadCore();
    }
}
