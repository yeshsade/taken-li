using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;
using TakenLi.Core;
using InputLanguage = TakenLi.Core.InputLanguage;

namespace TakenLi.Windows;

internal sealed class TextEditor
{
    internal bool Busy { get; private set; }

    internal async Task EditAsync(IntPtr target, TextAction action, AppSettings settings)
    {
        if (Busy) return;
        Busy = true;
        var copied = false;
        var wroteClipboard = false;
        uint ownSequence = 0;
        uint capturedSequence = 0;
        string? previousText = null;
        try
        {
            if (target == IntPtr.Zero) throw new InvalidOperationException("focus");
            if (Native.GetForegroundWindow() != target)
            {
                Native.SetForegroundWindow(target);
                await Task.Delay(120);
            }
            EnsureTarget(target);
            CheckEditable();
            for (var i = 0; i < 75 && ModifiersHeld(); i++) await Task.Delay(20);
            if (ModifiersHeld()) throw new InvalidOperationException("release");
            EnsureTarget(target);
            previousText = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            var source = Native.IsEnglish(target) ? InputLanguage.English : InputLanguage.Hebrew;
            var selection = InspectSelection();
            if (selection.State == SelectionState.Empty)
            {
                SelectScope(target, settings.UnselectedScope, selection.Caret);
                await Task.Delay(60);
            }
            var text = await CopyAsync(target);
            copied = text is not null;
            capturedSequence = Native.GetClipboardSequenceNumber();
            if (string.IsNullOrEmpty(text) && selection.State == SelectionState.Unknown)
            {
                SelectScope(target, settings.UnselectedScope, null);
                await Task.Delay(60);
                text = await CopyAsync(target);
                copied = text is not null;
                capturedSequence = Native.GetClipboardSequenceNumber();
            }
            if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("selection");
            var result = TextOperations.Apply(text, action, source);
            EnsureTarget(target);
            CheckEditable();
            Clipboard.SetText(result, TextDataFormat.UnicodeText);
            wroteClipboard = true;
            ownSequence = Native.GetClipboardSequenceNumber();
            Native.Chord(86);
            // Keep the replacement available while the target processes Ctrl+V.
            await Task.Delay(500);
            if (action == TextAction.FixLayout && Native.GetForegroundWindow() == target)
            {
                var language = TextOperations.ResultLanguage(result);
                if (language is not null && !Native.SwitchExistingLayout(target, language == InputLanguage.Hebrew))
                    throw new InvalidOperationException("layout");
            }
        }
        catch (ExternalException) { throw new InvalidOperationException("clipboard"); }
        finally
        {
            // Restore plain text only, and never overwrite a newer clipboard update.
            // Rich formats and undo remain optional, not silently promised.
            if (previousText is not null && copied && Native.GetClipboardSequenceNumber() == (wroteClipboard ? ownSequence : capturedSequence))
            {
                try { Clipboard.SetText(previousText, TextDataFormat.UnicodeText); }
                catch (ExternalException) { /* Restoration is optional. */ }
            }
            Busy = false;
        }
    }

    private enum SelectionState { Unknown, Empty, Present }

    private static (SelectionState State, TextPatternRange? Caret) InspectSelection()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null || !element.TryGetCurrentPattern(TextPattern.Pattern, out var value))
                return (SelectionState.Unknown, null);
            var pattern = (TextPattern)value;
            if (pattern.SupportedTextSelection == SupportedTextSelection.None) return (SelectionState.Unknown, null);
            var ranges = pattern.GetSelection();
            foreach (var range in ranges)
                if (range.CompareEndpoints(TextPatternRangeEndpoint.Start, range, TextPatternRangeEndpoint.End) != 0)
                    return (SelectionState.Present, null);
            return (SelectionState.Empty, ranges.Length == 1 ? ranges[0].Clone() : null);
        }
        catch (ElementNotAvailableException) { throw new InvalidOperationException("focus"); }
        catch (InvalidOperationException) { return (SelectionState.Unknown, null); }
        catch (NotSupportedException) { return (SelectionState.Unknown, null); }
    }

    private static void SelectScope(IntPtr target, SelectionScope scope, TextPatternRange? caret)
    {
        EnsureTarget(target);
        if (scope == SelectionScope.WholeField) { Native.Chord(65); return; }
        if (caret is not null)
        {
            try
            {
                // Ask the editor to select its current line. This avoids relying
                // on Word's or an RTL editor's interpretation of Home/End.
                caret.ExpandToEnclosingUnit(TextUnit.Line);
                var line = caret.GetText(-1);
                if (line.Trim('\r', '\n').Length == 0) throw new InvalidOperationException("selection");
                // Providers may expand an unsupported unit to a paragraph or
                // document. Never widen the user's line scope to several lines.
                if (line.TrimEnd('\r', '\n').IndexOfAny(['\r', '\n']) >= 0)
                    throw new NotSupportedException();
                EnsureTarget(target);
                caret.Select();
                return;
            }
            catch (ElementNotAvailableException) { throw new InvalidOperationException("focus"); }
            catch (InvalidOperationException error) when (error.Message != "selection") { /* Keyboard fallback below. */ }
            catch (NotSupportedException) { /* Keyboard fallback below. */ }
        }
        Native.SendKeys((36, false), (36, true));
        Native.Chord(35, shift: true);
    }

    private static async Task<string?> CopyAsync(IntPtr target)
    {
        EnsureTarget(target);
        var before = Native.GetClipboardSequenceNumber();
        Native.Chord(67);
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(20);
            EnsureTarget(target);
            if (Native.GetClipboardSequenceNumber() != before)
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        // Never interpret pre-existing clipboard content as the selected text.
        return null;
    }

    private static bool ModifiersHeld() => (Native.GetAsyncKeyState(16) & 0x8000) != 0 ||
        (Native.GetAsyncKeyState(17) & 0x8000) != 0 || (Native.GetAsyncKeyState(18) & 0x8000) != 0 ||
        (Native.GetAsyncKeyState(91) & 0x8000) != 0 || (Native.GetAsyncKeyState(92) & 0x8000) != 0;

    private static void EnsureTarget(IntPtr target)
    {
        if (Native.GetForegroundWindow() != target) throw new InvalidOperationException("focus");
    }

    private static void CheckEditable()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null) throw new InvalidOperationException("focus");
            if (element.Current.IsPassword) throw new InvalidOperationException("password");
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && ((ValuePattern)pattern).Current.IsReadOnly)
                throw new InvalidOperationException("readonly");
            var control = element.Current.ControlType;
            if (control == ControlType.Button || control == ControlType.Hyperlink || control == ControlType.MenuItem)
                throw new InvalidOperationException("readonly");
        }
        catch (ElementNotAvailableException) { throw new InvalidOperationException("focus"); }
    }
}
