using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class SettingsForm : Form
{
    private AppSettings _draft;
    private Ui _ui;
    private readonly Action<AppSettings> _save;
    private readonly Action _help;
    private Panel _pane = null!;
    private int _page;
    private bool _building;

    internal SettingsForm(AppSettings settings, Action<AppSettings> save, Action help)
    {
        _draft = settings.Copy();
        _ui = new Ui(_draft.Language);
        _save = save;
        _help = help;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(980, 700);
        MinimumSize = new Size(870, 640);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Ui.Surface;
        Build();
    }

    private void Build()
    {
        _building = true;
        SuspendLayout();
        foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
        _ui = new Ui(_draft.Language);
        Text = _ui.T("הגדרות", "Settings");
        RightToLeft = _ui.Direction;
        RightToLeftLayout = false;
        var root = Ui.Table(1, 3);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        var header = Ui.Table(2, 1);
        header.Padding = new Padding(28, 14, 28, 14);
        header.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Absolute : SizeType.Percent, _ui.Hebrew ? 205 : 100));
        header.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Percent : SizeType.Absolute, _ui.Hebrew ? 100 : 205));
        var heading = _ui.Label(Text, true, 48);
        heading.Font = new Font("Segoe UI", 21, FontStyle.Bold);
        header.Controls.Add(heading, _ui.Column(0, 2), 0);
        var languages = Ui.Table(1, 2);
        languages.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        languages.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        var languageLabel = _ui.Label(_ui.T("שפת הממשק", "Interface language"), height: 24);
        languageLabel.Margin = Padding.Empty;
        languages.Controls.Add(languageLabel, 0, 0);
        var language = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
            RightToLeft = _ui.Direction, AccessibleName = languageLabel.Text, Margin = new Padding(0, 2, 0, 0)
        };
        language.Items.AddRange(["עברית", "English"]);
        language.SelectedIndex = _ui.Hebrew ? 0 : 1;
        language.SelectedIndexChanged += (_, _) =>
        {
            if (_building) return;
            _draft.Language = language.SelectedIndex == 0 ? "he" : "en";
            BeginInvoke(new Action(Build));
        };
        languages.Controls.Add(language, 0, 1);
        header.Controls.Add(languages, _ui.Column(1, 2), 0);
        root.Controls.Add(header, 0, 0);

        var body = Ui.Table(2, 1);
        body.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Percent : SizeType.Absolute, _ui.Hebrew ? 100 : 204));
        body.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Absolute : SizeType.Percent, _ui.Hebrew ? 204 : 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var sidebar = Ui.Table(1, 5);
        sidebar.Name = "Sidebar";
        sidebar.Padding = new Padding(12, 16, 12, 16);
        sidebar.BackColor = Ui.Sidebar;
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var pageNames = new[] { _ui.T("פעולות ומקשים", "Actions and keys"), _ui.T("חזרה לעברית", "Return to Hebrew"), _ui.T("ללא טקסט מסומן", "Without a selection"), _ui.T("הפעלה וממשק", "Startup and interface") };
        for (var i = 0; i < pageNames.Length; i++)
        {
            var index = i;
            var button = _ui.Button(pageNames[i]);
            button.Name = "Page" + i;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 3, 0, 3);
            button.Padding = new Padding(10, 0, 10, 0);
            button.TextAlign = _ui.Alignment;
            button.FlatAppearance.BorderSize = _page == i ? 1 : 0;
            button.FlatAppearance.BorderColor = Ui.Accent;
            button.BackColor = _page == i ? Ui.Selected : Ui.Sidebar;
            button.ForeColor = Ui.Ink;
            button.Font = new Font(Font, _page == i ? FontStyle.Bold : FontStyle.Regular);
            button.Click += (_, _) => { _page = index; Build(); };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            sidebar.Controls.Add(button, 0, i);
        }
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(sidebar, _ui.Column(0, 2), 0);
        _pane = new Panel
        {
            Name = "ContentPane", Dock = DockStyle.Fill, AutoScroll = true,
            Padding = new Padding(28, 12, 28, 16), BackColor = Ui.Surface, RightToLeft = RightToLeft.No, Margin = Padding.Empty
        };
        body.Controls.Add(_pane, _ui.Column(1, 2), 0);
        root.Controls.Add(body, 0, 1);
        BuildPage(pageNames[_page]);

        var footer = Ui.Table(2, 1);
        footer.Padding = new Padding(28, 14, 28, 14);
        footer.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Absolute : SizeType.Percent, _ui.Hebrew ? 222 : 100));
        footer.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Percent : SizeType.Absolute, _ui.Hebrew ? 100 : 222));
        var hint = _ui.Label(_ui.T("השינויים יחולו לאחר לחיצה על שמירה.", "Changes apply when you select Save."), height: 38);
        hint.Font = new Font("Segoe UI", 9);
        hint.ForeColor = Ui.Muted;
        footer.Controls.Add(hint, _ui.Column(0, 2), 0);
        var buttons = Ui.Table(2, 1);
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var save = _ui.Button(_ui.T("שמירה", "Save"), true);
        save.Anchor = AnchorStyles.None;
        save.Click += (_, _) =>
        {
            try { _draft.Validate(); _save(_draft.Copy()); Close(); }
            catch (Exception error) { _ui.ShowError(error, this); }
        };
        var cancel = _ui.Button(_ui.T("ביטול", "Cancel"));
        cancel.Anchor = AnchorStyles.None;
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(save, _ui.Column(0, 2), 0);
        buttons.Controls.Add(cancel, _ui.Column(1, 2), 0);
        footer.Controls.Add(buttons, _ui.Column(1, 2), 0);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;
        ResumeLayout(true);
        _building = false;
    }

    private TableLayoutPanel Stack()
    {
        var stack = Ui.Table(1, 0);
        stack.Dock = DockStyle.Top;
        stack.AutoSize = true;
        stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _pane.Controls.Add(stack);
        return stack;
    }

    private static void Add(TableLayoutPanel stack, Control control)
    {
        var row = stack.RowCount;
        stack.RowCount = row + 1;
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(control, 0, row);
    }

    private void BuildPage(string title)
    {
        var stack = Stack();
        var heading = _ui.Label(title, true, 36);
        heading.Name = "PageHeading";
        heading.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        Add(stack, heading);
        if (_page == 0) BuildActions(stack);
        else if (_page == 1) BuildTimer(stack);
        else if (_page == 2) BuildScope(stack);
        else BuildGeneral(stack);
    }

    private Control Example(TextAction action)
    {
        var (from, to) = action switch { TextAction.FixLayout => ("akuo", "שלום"), TextAction.SwapCase => ("Hello", "hELLO"), _ => ("שלום", "םולש") };
        var example = Ui.Table(5, 1);
        example.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var widths = new[] { 66, 64, 24, 70, 0 };
        for (var physical = 0; physical < 5; physical++)
        {
            var logical = _ui.Column(physical, 5);
            example.ColumnStyles.Add(new ColumnStyle(logical == 4 ? SizeType.Percent : SizeType.Absolute, logical == 4 ? 100 : widths[logical]));
        }
        var texts = new[] { _ui.T("לדוגמה:", "Example:"), from, _ui.Hebrew ? "←" : "→", to };
        for (var i = 0; i < texts.Length; i++)
        {
            var label = _ui.Label(texts[i], height: 24);
            label.Margin = Padding.Empty;
            label.Font = new Font("Segoe UI", 9);
            label.ForeColor = Ui.Muted;
            if (i == 1 || i == 3)
            {
                label.RightToLeft = texts[i].Any(c => c is >= '\u05D0' and <= '\u05EA') ? RightToLeft.Yes : RightToLeft.No;
                label.TextAlign = ContentAlignment.MiddleCenter;
            }
            example.Controls.Add(label, _ui.Column(i, 5), 0);
        }
        return example;
    }

    private void BuildActions(TableLayoutPanel stack)
    {
        Add(stack, _ui.Paragraph(_ui.T("מקשים אלו הם ברירת המחדל, ותוכלו לשנות אותם בכל עת.", "These are the default keys. You can change them at any time.")));
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var row = Ui.Table(3, 1);
            row.Name = "ActionRow" + action;
            row.Dock = DockStyle.Top;
            row.Height = 82;
            row.Margin = new Padding(0, 2, 0, 2);
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var widths = new[] { 0, 132, 94 };
            for (var physical = 0; physical < 3; physical++)
            {
                var logical = _ui.Column(physical, 3);
                row.ColumnStyles.Add(new ColumnStyle(logical == 0 ? SizeType.Percent : SizeType.Absolute, logical == 0 ? 100 : widths[logical]));
            }
            var info = Ui.Table(1, 2);
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            info.Controls.Add(_ui.Label(_ui.ActionName(action), true, 34), 0, 0);
            info.Controls.Add(Example(action), 0, 1);
            row.Controls.Add(info, _ui.Column(0, 3), 0);
            var key = new Label
            {
                Name = "Key" + action, Text = _draft.KeyFor(action).Display, AutoSize = false,
                Size = new Size(120, 36), Anchor = AnchorStyles.None, TextAlign = ContentAlignment.MiddleCenter,
                RightToLeft = RightToLeft.No, Font = new Font("Consolas", 11), ForeColor = Ui.Ink,
                BackColor = Ui.KeySurface, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4)
            };
            key.AccessibleName = _ui.ActionName(action) + ": " + key.Text;
            row.Controls.Add(key, _ui.Column(1, 3), 0);
            var change = _ui.Button(_ui.T("שינוי…", "Change…"));
            change.Size = new Size(86, 36);
            change.Anchor = AnchorStyles.None;
            change.Margin = new Padding(4);
            change.AccessibleName = _ui.T("שינוי המקש עבור ", "Change key for ") + _ui.ActionName(action);
            change.Click += (_, _) =>
            {
                using var dialog = new KeyDialog(_ui, _ui.ActionName(action), _draft.KeyFor(action));
                if (dialog.ShowDialog(this) == DialogResult.OK) { _draft.SetKey(action, dialog.Result); key.Text = dialog.Result.Display; }
            };
            row.Controls.Add(change, _ui.Column(2, 3), 0);
            Add(stack, row);
            Add(stack, new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Ui.Divider, Margin = Padding.Empty });
        }
        Add(stack, _ui.Paragraph(_ui.T("הפעולה תשנה את הטקסט המסומן. כשאין סימון, היא תפעל לפי הבחירה בהגדרות.", "Actions change the selected text. Without a selection, the scope follows your settings.")));
        var summary = Ui.Table(2, 1);
        summary.Dock = DockStyle.Top;
        summary.Height = 80;
        summary.Padding = new Padding(12, 8, 12, 8);
        summary.BackColor = Ui.Sidebar;
        summary.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Absolute : SizeType.Percent, _ui.Hebrew ? 94 : 100));
        summary.ColumnStyles.Add(new ColumnStyle(_ui.Hebrew ? SizeType.Percent : SizeType.Absolute, _ui.Hebrew ? 100 : 94));
        var summaryText = _draft.AutoReturnEnabled
            ? _ui.T($"חזרה אוטומטית לעברית: פעילה\nאחרי {_draft.AutoReturnSeconds} שניות ללא הקלדה", $"Automatic return to Hebrew: On\nAfter {_draft.AutoReturnSeconds} seconds without typing")
            : _ui.T("חזרה אוטומטית לעברית: כבויה", "Automatic return to Hebrew: Off");
        summary.Controls.Add(_ui.Label(summaryText, height: 54), _ui.Column(0, 2), 0);
        var edit = _ui.Button(_ui.T("שינוי…", "Change…"));
        edit.Size = new Size(86, 36);
        edit.Anchor = AnchorStyles.None;
        edit.Click += (_, _) => { _page = 1; Build(); };
        summary.Controls.Add(edit, _ui.Column(1, 2), 0);
        Add(stack, summary);
    }

    private void BuildTimer(TableLayoutPanel stack)
    {
        var enabled = _ui.Check(_ui.T("לחזור לעברית אחרי הפסקה בהקלדה באנגלית", "Return to Hebrew after a pause in English typing"), _draft.AutoReturnEnabled);
        enabled.Name = "AutoReturnEnabled";
        Add(stack, enabled);
        Add(stack, _ui.Label(_ui.T("זמן ההמתנה בשניות", "Wait time in seconds")));
        var delay = new NumericUpDown
        {
            Minimum = 1, Maximum = 3600, Value = _draft.AutoReturnSeconds, Width = 120, Enabled = enabled.Checked,
            Anchor = _ui.Hebrew ? AnchorStyles.Right : AnchorStyles.Left, RightToLeft = RightToLeft.No,
            AccessibleName = _ui.T("זמן ההמתנה בשניות", "Wait time in seconds"), Margin = new Padding(0, 2, 0, 12)
        };
        delay.ValueChanged += (_, _) => _draft.AutoReturnSeconds = (int)delay.Value;
        enabled.CheckedChanged += (_, _) => { _draft.AutoReturnEnabled = enabled.Checked; delay.Enabled = enabled.Checked; };
        Add(stack, delay);
        Add(stack, _ui.Paragraph(_ui.T("כל הקלדה מתחילה את הספירה מחדש. נעשה שימוש רק בפריסות שכבר מותקנות. החזרה חלה על החלון הפעיל.", "Each keystroke restarts the timer. Only existing layouts are used. The return applies to the active window.")));
        var caps = _ui.Check(_ui.T("לכבות Caps Lock במעבר לעברית", "Turn off Caps Lock when switching to Hebrew"), _draft.DisableCapsLockOnHebrew);
        caps.Name = "DisableCapsLockOnHebrew";
        caps.CheckedChanged += (_, _) => _draft.DisableCapsLockOnHebrew = caps.Checked;
        Add(stack, caps);
        Add(stack, _ui.Paragraph(_ui.T("חל גם על מעבר לעברית באמצעות תיקון טקסט.", "Also applies when text correction switches to Hebrew.")));
        Add(stack, _ui.Label(_ui.T("תוכנות להחרגה (לא חובה)", "Excluded applications (optional)"), true));
        Add(stack, _ui.Paragraph(_ui.T("שם קובץ ההפעלה בכל שורה, למשל notepad.exe. בתוכנות אלה לא תהיה חזרה אוטומטית; פעולות התיקון יישארו זמינות.", "One executable name per line, e.g. notepad.exe. These apps will not return automatically; text actions remain available.")));
        var exclusions = new TextBox { Multiline = true, Height = 100, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, Text = string.Join(Environment.NewLine, _draft.ExcludedApplications), RightToLeft = RightToLeft.No, AccessibleName = _ui.T("תוכנות להחרגה", "Excluded applications") };
        exclusions.TextChanged += (_, _) => _draft.ExcludedApplications = exclusions.Lines.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Add(stack, exclusions);
    }

    private void BuildScope(TableLayoutPanel stack)
    {
        Add(stack, _ui.Paragraph(_ui.T("טקסט שסימנתם יקבל תמיד קדימות. כשאין סימון, בחרו על מה תחול הפעולה:", "Selected text always takes priority. Without a selection, choose the action scope:")));
        var line = new RadioButton { Text = _ui.T("השורה הנוכחית", "Current line"), AutoSize = true, Checked = _draft.UnselectedScope == SelectionScope.CurrentLine, RightToLeft = _ui.Direction, ForeColor = Ui.Ink, Anchor = _ui.Hebrew ? AnchorStyles.Right : AnchorStyles.Left, Margin = new Padding(0, 12, 0, 12) };
        var field = new RadioButton { Text = _ui.T("כל הטקסט בשדה", "All text in the field"), AutoSize = true, Checked = _draft.UnselectedScope == SelectionScope.WholeField, RightToLeft = _ui.Direction, ForeColor = Ui.Ink, Anchor = _ui.Hebrew ? AnchorStyles.Right : AnchorStyles.Left, Margin = new Padding(0, 12, 0, 12) };
        line.CheckedChanged += (_, _) => { if (line.Checked) _draft.UnselectedScope = SelectionScope.CurrentLine; };
        field.CheckedChanged += (_, _) => { if (field.Checked) _draft.UnselectedScope = SelectionScope.WholeField; };
        Add(stack, line);
        Add(stack, field);
        Add(stack, _ui.Paragraph(_ui.T("אם הפעולה לא נתמכת בתוכנה מסוימת, נסו לסמן את הטקסט קודם. שדות סיסמה אינם נערכים.", "If an application does not support this action, select the text first. Password fields are not edited.")));
    }

    private void BuildGeneral(TableLayoutPanel stack)
    {
        var startup = _ui.Check(_ui.T("להפעיל עם Windows", "Start with Windows"), _draft.StartWithWindows);
        startup.CheckedChanged += (_, _) => _draft.StartWithWindows = startup.Checked;
        Add(stack, startup);
        var welcome = _ui.Check(_ui.T("להציג הסבר בהפעלת התוכנה", "Show an explanation on startup"), _draft.ShowWelcome);
        welcome.CheckedChanged += (_, _) => _draft.ShowWelcome = welcome.Checked;
        Add(stack, welcome);
        var help = _ui.Button(_ui.T("פתיחת ההסבר", "Open explanation"));
        help.Width = 150;
        help.Anchor = _ui.Hebrew ? AnchorStyles.Right : AnchorStyles.Left;
        help.Click += (_, _) => _help();
        Add(stack, help);
        Add(stack, _ui.Paragraph(_ui.T("התוכנה פועלת ללא אינטרנט. נשמרות הגדרות בלבד, ללא היסטוריית טקסטים. מעבר שפה נעשה רק בין הפריסות הקיימות במחשב.", "The app works offline. Only settings are saved, with no text history. Language switching uses only layouts already on this computer.")));
    }
}
