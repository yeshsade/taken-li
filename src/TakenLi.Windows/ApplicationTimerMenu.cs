using System;
using System.Linq;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed record ApplicationTarget(string Executable, string DisplayName);

internal static class ApplicationTimerMenu
{
    internal static void AddTo(ContextMenuStrip menu, Ui ui, Func<ApplicationTarget?> target,
        Func<AppSettings> settings, Action<AppSettings> save, Action<Exception> report, Func<bool> canEdit)
    {
        menu.Items.Add(new ToolStripMenuItem(ui.T("התוכנה שנבחרה", "Selected application")) { Enabled = false });
        var name = new ToolStripMenuItem { Name = "ApplicationName", Enabled = false, RightToLeft = RightToLeft.No };
        var action = new ToolStripMenuItem { Name = "ApplicationTimerAction" };
        var globalOff = new ToolStripMenuItem(ui.T("הטיימר הכללי כבוי בהגדרות", "The global timer is off in Settings")) { Name = "GlobalTimerOff", Enabled = false };
        menu.Items.Add(name);
        menu.Items.Add(action);
        menu.Items.Add(globalOff);
        ApplicationTarget? selected = null;
        bool enable = false;
        menu.Opening += (_, _) =>
        {
            // Freeze the target for this menu opening. Focus can move to the
            // taskbar/menu while the user reads and clicks the action.
            selected = target();
            var current = settings();
            name.Text = selected?.DisplayName ?? ui.T("פתחו תחילה את התוכנה הרצויה", "First open the application you want");
            name.RightToLeft = selected is null ? ui.Direction : RightToLeft.No;
            enable = selected is not null && current.ExcludedApplications.Any(x => string.Equals(x, selected.Executable, StringComparison.OrdinalIgnoreCase));
            action.Text = enable
                ? ui.T("להפעיל חזרה אוטומטית לעברית", "Enable automatic return to Hebrew")
                : ui.T("לכבות חזרה אוטומטית לעברית", "Turn off automatic return to Hebrew");
            action.Enabled = selected is not null && canEdit();
            globalOff.Visible = !current.AutoReturnEnabled;
        };
        action.Click += (_, _) =>
        {
            if (selected is null || !canEdit()) return;
            var next = settings().Copy();
            next.ExcludedApplications.RemoveAll(x => string.Equals(x, selected.Executable, StringComparison.OrdinalIgnoreCase));
            if (!enable) next.ExcludedApplications.Add(selected.Executable);
            try { save(next); }
            catch (Exception error) { report(error); }
        };
    }
}
