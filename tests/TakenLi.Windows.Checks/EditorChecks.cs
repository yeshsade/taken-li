using System.Diagnostics;
using System.Text.Json;
using TakenLi.Core;

namespace TakenLi.Windows.Checks;

// The editor must target another process: UI Automation against controls on the
// calling UI thread is not representative of the application's real workflow.
internal static class EditorChecks
{
    private sealed record Scenario(string Text, int Start, int Length, string Expected, SelectionScope Scope, TextAction Action, bool Hotkey = false, bool SingleLine = false, bool Qt = false);
    private sealed record Snapshot(long Window, string Text, int Start, int Length);

    internal static int Host(string directory)
    {
        var scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(Path.Combine(directory, "scenario.json")))!;
        using var form = new Form { Text = "TakenLi isolated editor test", ClientSize = new Size(600, 240), StartPosition = FormStartPosition.CenterScreen };
        using var box = new TextBox { Multiline = !scenario.SingleLine, Dock = DockStyle.Fill, Text = scenario.Text, Font = new Font("Segoe UI", 12), AcceptsReturn = !scenario.SingleLine };
        form.Controls.Add(box);
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            var snapshot = new Snapshot(form.Handle.ToInt64(), box.Text, box.SelectionStart, box.SelectionLength);
            // Atomic replacement prevents reading a partially written snapshot.
            var temp = Path.Combine(directory, "snapshot.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
            File.Move(temp, Path.Combine(directory, "snapshot.json"), true);
            if (File.Exists(Path.Combine(directory, "stop"))) form.Close();
        };
        form.Shown += (_, _) =>
        {
            form.Activate();
            box.Focus();
            box.Select(scenario.Start, scenario.Length);
            timer.Start();
        };
        Application.Run(form);
        return 0;
    }

    internal static void Run()
    {
        // Use a real outer message loop. Calling DoEvents without one tears
        // down the WinForms synchronization context between asynchronous waits.
        Exception? failure = null;
        using var coordinator = new Form { WindowState = FormWindowState.Minimized, ShowInTaskbar = false };
        coordinator.Shown += (_, _) =>
        {
            try { RunScenarios(); }
            catch (Exception error) { failure = error; }
            finally { coordinator.Close(); }
        };
        Application.Run(coordinator);
        if (failure is not null) throw new InvalidOperationException(failure.Message, failure);
    }

    private static void RunScenarios()
    {
        var scenarios = new[]
        {
            new Scenario("Hello", 5, 0, "hELLO", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("first\r\nHello\r\nlast", 12, 0, "first\r\nhELLO\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("first\r\nHello\r\nlast", 9, 0, "first\r\nhELLO\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("Hello\r\nWorld", 4, 0, "hELLO\r\nwORLD", SelectionScope.WholeField, TextAction.SwapCase),
            new Scenario("Hello World", 0, 5, "hELLO World", SelectionScope.WholeField, TextAction.SwapCase),
            new Scenario("שלום\r\nעולם", 4, 0, "םולש\r\nעולם", SelectionScope.CurrentLine, TextAction.Reverse),
            new Scenario("MyFile.txt", 10, 0, "mYfILE.TXT", SelectionScope.CurrentLine, TextAction.SwapCase, SingleLine: true),
            new Scenario("MyFile.txt", 0, 6, "mYfILE.txt", SelectionScope.WholeField, TextAction.SwapCase, SingleLine: true),
            new Scenario("akuo", 4, 0, "שלום", SelectionScope.CurrentLine, TextAction.FixLayout, Hotkey: true),
            new Scenario("akuo other", 0, 4, "שלום other", SelectionScope.WholeField, TextAction.FixLayout, Hotkey: true),
            new Scenario("akuo", 0, 4, "שלום", SelectionScope.CurrentLine, TextAction.FixLayout, Hotkey: true, SingleLine: true, Qt: true),
            new Scenario("akuo other", 0, 4, "שלום other", SelectionScope.WholeField, TextAction.FixLayout, Hotkey: true, Qt: true),
            new Scenario("Hello", 5, 0, "hELLO", SelectionScope.CurrentLine, TextAction.SwapCase, SingleLine: true, Qt: true),
            new Scenario("first\r\n\r\nlast", 7, 0, "first\r\n\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase)
        };
        var original = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        var originalCaps = Native.CapsLockOn;
        try
        {
            for (var index = 0; index < scenarios.Length; index++)
            {
                var directory = Path.Combine(Path.GetTempPath(), "taken-li-editor-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var scenario = scenarios[index];
                File.WriteAllText(Path.Combine(directory, "scenario.json"), JsonSerializer.Serialize(scenario));
                var info = new ProcessStartInfo(scenario.Qt ? "python" : Environment.ProcessPath!) { UseShellExecute = false };
                info.ArgumentList.Add(scenario.Qt ? Path.GetFullPath("tests/TakenLi.Windows.Checks/qt_editor_host.py") : "--editor-host");
                info.ArgumentList.Add(directory);
                using var process = Process.Start(info)!;
                try
                {
                    AwaitSnapshot(directory, _ => true);
                    var snapshot = Read(directory);
                    var target = new IntPtr(snapshot.Window);
                    Native.SetForegroundWindow(target);
                    PumpUntil(() => Native.GetForegroundWindow() == target, TimeSpan.FromSeconds(3));
                    if (index == 0) CheckCapsLock(target);
                    Clipboard.SetText("PREVIOUS CLIPBOARD — must never be edited");
                    var editor = new TextEditor();
                    Exception? failure = null;
                    var settings = new AppSettings { UnselectedScope = scenario.Scope };
                    Task? task = null;
                    using var hotkeys = new HotkeyWindow();
                    if (scenario.Hotkey)
                    {
                        hotkeys.ActionRequested += action => task = editor.EditAsync(target, action, settings);
                        hotkeys.Apply(settings);
                        Native.SendKeys((121, false), (121, true)); // Actual registered F10.
                        PumpUntil(() => task is not null, TimeSpan.FromSeconds(3));
                    }
                    else task = editor.EditAsync(target, scenario.Action, settings);
                    var editing = task ?? throw new InvalidOperationException("No editor task started");
                    PumpUntil(() => editing.IsCompleted, TimeSpan.FromSeconds(5));
                    try { editing.GetAwaiter().GetResult(); }
                    catch (InvalidOperationException error) { failure = error; }
                    if (index == scenarios.Length - 1)
                    {
                        if (failure?.Message != "selection") throw new InvalidOperationException("Empty line must report no editable text");
                    }
                    else if (failure is not null && !(scenario.Action == TextAction.FixLayout && failure.Message == "layout" && !HasHebrewLayout()))
                        throw new InvalidOperationException($"Editor scenario {index}: {failure.Message}", failure);
                    AwaitSnapshot(directory, state => state.Text == scenario.Expected);
                    if (Clipboard.GetText() != "PREVIOUS CLIPBOARD — must never be edited") throw new InvalidOperationException("Editor did not preserve the previous text clipboard");
                    if (editor.Busy) throw new InvalidOperationException("Editor remained busy after completion");
                    Console.WriteLine($"PASS actual editor scenario {index}: scope={scenario.Scope}, selection={scenario.Length}, action={scenario.Action}, hotkey={scenario.Hotkey}");
                }
                catch (Exception error)
                {
                    var snapshot = Read(directory);
                    throw new InvalidOperationException($"Editor scenario {index} failed ({error.Message}): expected {JsonSerializer.Serialize(scenario.Expected)}, actual {JsonSerializer.Serialize(snapshot.Text)}, selection {snapshot.Start}/{snapshot.Length}", error);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(directory, "stop"), "");
                    if (!process.WaitForExit(2000)) process.Kill(true);
                    Directory.Delete(directory, true);
                }
            }
        }
        finally
        {
            if (original is not null) Clipboard.SetText(original);
            if (Native.CapsLockOn != originalCaps)
            {
                Native.SendKeys((0x14, false), (0x14, true));
                PumpUntil(() => Native.CapsLockOn == originalCaps, TimeSpan.FromSeconds(2));
            }
        }
    }

    private static Snapshot Read(string directory)
    {
        using var stream = new FileStream(Path.Combine(directory, "snapshot.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<Snapshot>(stream)!;
    }
    private static bool HasHebrewLayout()
    {
        var layouts = new IntPtr[Native.GetKeyboardLayoutList(0, null)];
        var count = Native.GetKeyboardLayoutList(layouts.Length, layouts);
        return layouts.Take(count).Any(layout => (layout.ToInt64() & 0xffff) == 0x040d);
    }
    private static void CheckCapsLock(IntPtr target)
    {
        var original = Native.CapsLockOn;
        try
        {
            if (!Native.CapsLockOn) Native.SendKeys((0x14, false), (0x14, true));
            PumpUntil(() => Native.CapsLockOn, TimeSpan.FromSeconds(2));
            if (!Native.SwitchExistingLayout(target, false, true)) throw new InvalidOperationException("Existing English layout missing in Caps Lock check");
            Application.DoEvents();
            if (!Native.CapsLockOn) throw new InvalidOperationException("Switching to English must preserve Caps Lock");
            if (HasHebrewLayout())
            {
                Native.SwitchExistingLayout(target, true, false);
                Application.DoEvents();
                if (!Native.CapsLockOn) throw new InvalidOperationException("Disabled preference must preserve Caps Lock");
                Native.SwitchExistingLayout(target, true, true);
                PumpUntil(() => !Native.CapsLockOn, TimeSpan.FromSeconds(2));
                Native.SwitchExistingLayout(target, false, true);
            }
            else
            {
                if (Native.SwitchExistingLayout(target, true, true)) throw new InvalidOperationException("Missing Hebrew layout must not be reported as available");
                if (!Native.CapsLockOn) throw new InvalidOperationException("Unavailable Hebrew layout must not turn Caps Lock off");
                Console.WriteLine("Hebrew layout transition check not run: no Hebrew layout installed. No layout was added.");
            }
            Native.DisableCapsLockIfOn();
            PumpUntil(() => !Native.CapsLockOn, TimeSpan.FromSeconds(2));
            Native.DisableCapsLockIfOn();
            Application.DoEvents();
            if (Native.CapsLockOn) throw new InvalidOperationException("Disabling an already-off Caps Lock must leave it off");
            Console.WriteLine("PASS Caps Lock: actual toggle off, already-off state, English and unavailable-layout behavior");
        }
        finally
        {
            if (Native.CapsLockOn != original)
            {
                Native.SendKeys((0x14, false), (0x14, true));
                PumpUntil(() => Native.CapsLockOn == original, TimeSpan.FromSeconds(2));
            }
        }
    }
    private static void AwaitSnapshot(string directory, Func<Snapshot, bool> condition) => PumpUntil(() => File.Exists(Path.Combine(directory, "snapshot.json")) && condition(Read(directory)), TimeSpan.FromSeconds(4));
    private static void PumpUntil(Func<bool> done, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.Elapsed > timeout) throw new TimeoutException("Windows editor check timed out");
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
