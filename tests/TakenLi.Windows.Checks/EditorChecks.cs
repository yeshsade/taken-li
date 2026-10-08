using System.Diagnostics;
using System.Text.Json;
using TakenLi.Core;

namespace TakenLi.Windows.Checks;

// The editor must target another process: UI Automation against controls on the
// calling UI thread is not representative of the application's real workflow.
internal static class EditorChecks
{
    private sealed record Scenario(string Text, int Start, int Length, string Expected, SelectionScope Scope, TextAction Action);
    private sealed record Snapshot(long Window, string Text, int Start, int Length);

    internal static int Host(string directory)
    {
        var scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(Path.Combine(directory, "scenario.json")))!;
        using var form = new Form { Text = "TakenLi isolated editor test", ClientSize = new Size(600, 240), StartPosition = FormStartPosition.CenterScreen };
        using var box = new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = scenario.Text, Font = new Font("Segoe UI", 12), AcceptsReturn = true };
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
        var scenarios = new[]
        {
            new Scenario("Hello", 5, 0, "hELLO", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("first\r\nHello\r\nlast", 12, 0, "first\r\nhELLO\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("first\r\nHello\r\nlast", 9, 0, "first\r\nhELLO\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase),
            new Scenario("Hello\r\nWorld", 4, 0, "hELLO\r\nwORLD", SelectionScope.WholeField, TextAction.SwapCase),
            new Scenario("Hello World", 0, 5, "hELLO World", SelectionScope.WholeField, TextAction.SwapCase),
            new Scenario("שלום\r\nעולם", 4, 0, "םולש\r\nעולם", SelectionScope.CurrentLine, TextAction.Reverse),
            new Scenario("first\r\n\r\nlast", 7, 0, "first\r\n\r\nlast", SelectionScope.CurrentLine, TextAction.SwapCase)
        };
        var original = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        try
        {
            for (var index = 0; index < scenarios.Length; index++)
            {
                var directory = Path.Combine(Path.GetTempPath(), "taken-li-editor-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var scenario = scenarios[index];
                File.WriteAllText(Path.Combine(directory, "scenario.json"), JsonSerializer.Serialize(scenario));
                var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
                info.ArgumentList.Add("--editor-host");
                info.ArgumentList.Add(directory);
                using var process = Process.Start(info)!;
                try
                {
                    AwaitSnapshot(directory, _ => true);
                    var snapshot = Read(directory);
                    var target = new IntPtr(snapshot.Window);
                    Native.SetForegroundWindow(target);
                    PumpUntil(() => Native.GetForegroundWindow() == target, TimeSpan.FromSeconds(3));
                    Clipboard.SetText("PREVIOUS CLIPBOARD — must never be edited");
                    var editor = new TextEditor();
                    Exception? failure = null;
                    var task = editor.EditAsync(target, scenario.Action, new AppSettings { UnselectedScope = scenario.Scope });
                    PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(5));
                    try { task.GetAwaiter().GetResult(); }
                    catch (InvalidOperationException error) { failure = error; }
                    if (index == scenarios.Length - 1)
                    {
                        if (failure?.Message != "selection") throw new InvalidOperationException("Empty line must report no editable text");
                    }
                    else if (failure is not null) throw new InvalidOperationException($"Editor scenario {index}: {failure.Message}", failure);
                    AwaitSnapshot(directory, state => state.Text == scenario.Expected);
                    if (Clipboard.GetText() != "PREVIOUS CLIPBOARD — must never be edited") throw new InvalidOperationException("Editor did not preserve the previous text clipboard");
                    if (editor.Busy) throw new InvalidOperationException("Editor remained busy after completion");
                    Console.WriteLine($"PASS actual editor scenario {index}: scope={scenario.Scope}, selection={scenario.Length}, action={scenario.Action}");
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
        }
    }

    private static Snapshot Read(string directory) => JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(directory, "snapshot.json")))!;
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
