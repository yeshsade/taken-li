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
        ClientSize = new Size(940, 650);
        MinimumSize = new Size(810, 570);
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
        RightToLeftLayout = _ui.Hebrew;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, RightToLeft = _ui.Direction };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(26, 16, 26, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        var heading = _ui.Label(Text, true, 48);
        heading.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        header.Controls.Add(heading, 0, 0);
        var languages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, RightToLeft = _ui.Direction };
        languages.Controls.Add(new Label { Text = _ui.T("שפת הממשק", "Interface language"), AutoSize = true });
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, AccessibleName = _ui.T("שפת הממשק", "Interface language") };
        language.Items.AddRange(["עברית", "English"]);
        language.SelectedIndex = _ui.Hebrew ? 0 : 1;
        language.SelectedIndexChanged += (_, _) =>
        {
            if (_building) return;
            _draft.Language = language.SelectedIndex == 0 ? "he" : "en";
            BeginInvoke(new Action(Build));
        };
        languages.Controls.Add(language);
        header.Controls.Add(languages, 1, 0);
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12, 20, 12, 0), BackColor = Ui.Sidebar };
        var pageNames = new[] { _ui.T("פעולות ומקשים", "Actions and keys"), _ui.T("חזרה לעברית", "Return to Hebrew"), _ui.T("ללא טקסט מסומן", "Without a selection"), _ui.T("הפעלה וממשק", "Startup and interface") };
        for (var i = 0; i < pageNames.Length; i++)
        {
            var index = i;
            var button = _ui.Button(pageNames[i]);
            button.Dock = DockStyle.Fill;
            button.Height = 45;
            button.TextAlign = _ui.Alignment;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = _page == i ? Ui.Selected : sidebar.BackColor;
            button.ForeColor = _page == i ? Ui.Accent : Color.FromArgb(70, 78, 88);
            button.Click += (_, _) => { _page = index; Build(); };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            sidebar.Controls.Add(button, 0, i);
        }
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(sidebar, 0, 0);
        _pane = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(26, 20, 26, 20), BackColor = Ui.Surface };
        body.Controls.Add(_pane, 1, 0);
        root.Controls.Add(body, 0, 1);
        BuildPage(pageNames[_page]);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20, 8, 20, 8), BackColor = Ui.Surface };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        var hint = _ui.Label(_ui.T("השינויים יחולו לאחר לחיצה על שמירה.", "Changes apply when you select Save."), height: 38);
        hint.Font = new Font("Segoe UI", 9);
        hint.ForeColor = Ui.Muted;
        footer.Controls.Add(hint, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, RightToLeft = RightToLeft.No };
        var save = _ui.Button(_ui.T("שמירה", "Save"), true);
        save.Click += (_, _) =>
        {
            try { _draft.Validate(); _save(_draft.Copy()); Close(); }
            catch (Exception error) { _ui.ShowError(error, this); }
        };
        var cancel = _ui.Button(_ui.T("ביטול", "Cancel"));
        cancel.Click += (_, _) => Close();
        buttons.Controls.AddRange([save, cancel]);
        footer.Controls.Add(buttons, 1, 0);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;
        ResumeLayout(true);
        _building = false;
    }

    private TableLayoutPanel Stack()
    {
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RightToLeft = _ui.Direction };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _pane.Controls.Add(stack);
        return stack;
    }

    private static void Add(TableLayoutPanel stack, Control control)
    {
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(control, 0, stack.RowCount++);
    }

    private void BuildPage(string title)
    {
        var stack = Stack();
        Add(stack, _ui.Label(title, true, 35));
        if (_page == 0) BuildActions(stack);
        else if (_page == 1) BuildTimer(stack);
        else if (_page == 2) BuildScope(stack);
        else BuildGeneral(stack);
    }

    private void BuildActions(TableLayoutPanel stack)
    {
        Add(stack, _ui.Label(_ui.T("בחר את המקש שמפעיל כל פעולה בכל תוכנה.", "Choose a key for each action across applications."), height: 45));
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, ColumnCount = 3, Padding = new Padding(0, 14, 0, 14), Margin = new Padding(0, 2, 0, 2), RightToLeft = _ui.Direction };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            info.Controls.Add(_ui.Label(_ui.ActionName(action), true, 34));
            var (from, to) = action switch { TextAction.FixLayout => ("akuo", "שלום"), TextAction.SwapCase => ("Hello", "hELLO"), _ => ("שלום", "םולש") };
            var example = _ui.Label(_ui.T("לדוגמה: ", "Example: ") + "\u2066" + from + "\u2069 " + (_ui.Hebrew ? "←" : "→") + " \u2066" + to + "\u2069", height: 26);
            example.Font = new Font("Segoe UI", 9);
            example.ForeColor = Ui.Muted;
            info.Controls.Add(example);
            row.Controls.Add(info, 0, 0);
            var key = new Label { Text = _draft.KeyFor(action).Display, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No, Font = new Font("Consolas", 10), BackColor = Ui.KeySurface, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(5, 9, 5, 9) };
            key.AccessibleName = _ui.ActionName(action) + ": " + key.Text;
            row.Controls.Add(key, 1, 0);
            var change = _ui.Button(_ui.T("שינוי…", "Change…"));
            change.Dock = DockStyle.Fill;
            change.Margin = new Padding(5, 9, 5, 9);
            change.AccessibleName = _ui.T("שינוי המקש עבור ", "Change key for ") + _ui.ActionName(action);
            change.Click += (_, _) =>
            {
                using var dialog = new KeyDialog(_ui, _ui.ActionName(action), _draft.KeyFor(action));
                if (dialog.ShowDialog(this) == DialogResult.OK) { _draft.SetKey(action, dialog.Result); key.Text = dialog.Result.Display; }
            };
            row.Controls.Add(change, 2, 0);
            Add(stack, row);
            Add(stack, new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Ui.Border });
        }
        Add(stack, _ui.Label(_ui.T("הפעולה חלה על הטקסט המסומן. אפשר לבחור מה יקרה כשאין סימון.", "Actions apply to selected text. Choose what happens without a selection."), height: 58));
        var summary = _ui.Button(_draft.AutoReturnEnabled
            ? _ui.T($"חזרה אוטומטית לעברית: אחרי {_draft.AutoReturnSeconds} שניות ללא הקלדה — שינוי…", $"Return to Hebrew: after {_draft.AutoReturnSeconds}s without typing — Change…")
            : _ui.T("חזרה אוטומטית לעברית: כבויה — שינוי…", "Automatic return to Hebrew: Off — Change…"));
        summary.Dock = DockStyle.Top;
        summary.Height = 55;
        summary.TextAlign = _ui.Alignment;
        summary.Click += (_, _) => { _page = 1; Build(); };
        Add(stack, summary);
    }

    private void BuildTimer(TableLayoutPanel stack)
    {
        var enabled = _ui.Check(_ui.T("לחזור לעברית אחרי הפסקה בהקלדה באנגלית", "Return to Hebrew after a pause in English typing"), _draft.AutoReturnEnabled);
        Add(stack, enabled);
        Add(stack, _ui.Label(_ui.T("זמן ההמתנה בשניות", "Wait time in seconds")));
        var delay = new NumericUpDown { Minimum = 1, Maximum = 3600, Value = _draft.AutoReturnSeconds, Width = 120, Enabled = enabled.Checked, AccessibleName = _ui.T("זמן ההמתנה בשניות", "Wait time in seconds") };
        delay.ValueChanged += (_, _) => _draft.AutoReturnSeconds = (int)delay.Value;
        enabled.CheckedChanged += (_, _) => { _draft.AutoReturnEnabled = enabled.Checked; delay.Enabled = enabled.Checked; };
        Add(stack, delay);
        Add(stack, _ui.Label(_ui.T("כל הקלדה מתחילה את הספירה מחדש. נעשה שימוש רק בפריסות שכבר מותקנות. החזרה חלה על החלון הפעיל.", "Each keystroke restarts the timer. Only existing layouts are used. The return applies to the active window."), height: 85));
        Add(stack, _ui.Label(_ui.T("תוכנות להחרגה (לא חובה)", "Excluded applications (optional)"), true));
        Add(stack, _ui.Label(_ui.T("שם קובץ ההפעלה בכל שורה, למשל notepad.exe. בתוכנות אלה לא תהיה חזרה אוטומטית; פעולות התיקון יישארו זמינות.", "One executable name per line, e.g. notepad.exe. These apps will not return automatically; text actions remain available."), height: 75));
        var exclusions = new TextBox { Multiline = true, Height = 100, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, Text = string.Join(Environment.NewLine, _draft.ExcludedApplications), RightToLeft = RightToLeft.No, AccessibleName = _ui.T("תוכנות להחרגה", "Excluded applications") };
        exclusions.TextChanged += (_, _) => _draft.ExcludedApplications = exclusions.Lines.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Add(stack, exclusions);
    }

    private void BuildScope(TableLayoutPanel stack)
    {
        Add(stack, _ui.Label(_ui.T("טקסט שסימנת יקבל תמיד קדימות. כשאין סימון, בחר על מה תחול הפעולה:", "Selected text always takes priority. Without a selection, choose the action scope:"), height: 65));
        var line = new RadioButton { Text = _ui.T("השורה הנוכחית", "Current line"), AutoSize = true, Checked = _draft.UnselectedScope == SelectionScope.CurrentLine, Margin = new Padding(0, 12, 0, 12) };
        var field = new RadioButton { Text = _ui.T("כל הטקסט בשדה", "All text in the field"), AutoSize = true, Checked = _draft.UnselectedScope == SelectionScope.WholeField, Margin = new Padding(0, 12, 0, 12) };
        line.CheckedChanged += (_, _) => { if (line.Checked) _draft.UnselectedScope = SelectionScope.CurrentLine; };
        field.CheckedChanged += (_, _) => { if (field.Checked) _draft.UnselectedScope = SelectionScope.WholeField; };
        Add(stack, line);
        Add(stack, field);
        Add(stack, _ui.Label(_ui.T("אם הפעולה לא נתמכת בתוכנה מסוימת, נסה לסמן את הטקסט קודם. שדות סיסמה אינם נערכים.", "If an application does not support this action, select the text first. Password fields are not edited."), height: 80));
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
        help.Click += (_, _) => _help();
        Add(stack, help);
        Add(stack, _ui.Label(_ui.T("התוכנה פועלת ללא אינטרנט. נשמרות הגדרות בלבד, ללא היסטוריית טקסטים. מעבר שפה נעשה רק בין הפריסות הקיימות במחשב.", "The app works offline. Only settings are saved, with no text history. Language switching uses only layouts already on this computer."), height: 110));
    }
}
