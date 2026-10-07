using System;
using System.Drawing;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class WelcomeForm : Form
{
    internal bool DoNotShowAgain { get; private set; }
    internal bool OpenSettings { get; private set; }

    internal WelcomeForm(AppSettings settings)
    {
        var ui = new Ui(settings.Language);
        Text = ui.T("איך משתמשים בתוכנה", "How to use the app");
        ClientSize = new Size(760, 700);
        MinimumSize = new Size(680, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Ui.Surface;
        RightToLeft = ui.Direction;
        RightToLeftLayout = false;
        var root = Ui.Table(1, 2);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28, 18, 28, 8), RightToLeft = RightToLeft.No, Margin = Padding.Empty };
        var content = Ui.Table(1, 0);
        content.Dock = DockStyle.Top;
        content.AutoSize = true;
        content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control control)
        {
            var row = content.RowCount;
            content.RowCount = row + 1;
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.Controls.Add(control, 0, row);
        }
        void Paragraph(string he, string en, bool bold = false) => Add(ui.Paragraph(ui.T(he, en), bold));
        void Section(string he, string en)
        {
            var heading = ui.Paragraph(ui.T(he, en), true);
            heading.Margin = new Padding(0, 18, 0, 6);
            Add(heading);
        }

        var title = ui.Paragraph(ui.T("כתבתם בטעות באנגלית במקום בעברית?!", "Typed in English instead of Hebrew?!"), true);
        title.Font = new Font("Segoe UI", 17, FontStyle.Bold);
        Add(title);
        Paragraph("לא נורא — לחיצה אחת והטקסט מתוקן.", "No problem — one keypress corrects the text.");
        Paragraph("צריכים להפוך אותיות גדולות לקטנות, או להפך?\nצריכים להפוך את סדר האותיות?", "Need to change uppercase to lowercase, or the other way round?\nNeed to reverse the order of the letters?");
        Paragraph("מקש אחד והכול מסתדר.", "One key makes it simple.", true);

        Section("הפעולות והמקשים", "Actions and keys");
        Paragraph("סמנו את הטקסט שתרצו לשנות ולחצו על המקש המתאים:", "Select the text you want to change and press the appropriate key:");
        foreach (var action in Enum.GetValues<TextAction>())
        {
            var description = action switch
            {
                TextAction.FixLayout => ui.T("תיקון בין עברית לאנגלית, בשני הכיוונים", "Fix Hebrew and English, in either direction"),
                TextAction.SwapCase => ui.T("החלפת אותיות גדולות לקטנות ולהפך באנגלית", "Swap uppercase and lowercase in English"),
                _ => ui.T("היפוך סדר האותיות, למשל מ־״שלום״ ל־״םולש״", "Reverse letter order, for example from “abc” to “cba”")
            };
            var row = Ui.Table(2, 1);
            row.Dock = DockStyle.Top;
            row.Height = 46;
            row.ColumnStyles.Add(new ColumnStyle(ui.Hebrew ? SizeType.Absolute : SizeType.Percent, ui.Hebrew ? 136 : 100));
            row.ColumnStyles.Add(new ColumnStyle(ui.Hebrew ? SizeType.Percent : SizeType.Absolute, ui.Hebrew ? 100 : 136));
            row.Controls.Add(ui.Label(description, height: 36), ui.Column(0, 2), 0);
            var key = new Label
            {
                Text = settings.KeyFor(action).Display, Size = new Size(124, 32), Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No, BackColor = Ui.KeySurface,
                ForeColor = Ui.Ink, Font = new Font("Consolas", 10), AccessibleName = description + ": " + settings.KeyFor(action).Display
            };
            row.Controls.Add(key, ui.Column(1, 2), 0);
            Add(row);
        }
        var defaults = new AppSettings();
        var defaultKeys = settings.FixLayoutKey == defaults.FixLayoutKey && settings.SwapCaseKey == defaults.SwapCaseKey && settings.ReverseKey == defaults.ReverseKey;
        Paragraph(defaultKeys ? "מקשים אלו הם ברירת המחדל, ותוכלו לשנות אותם בכל עת בהגדרות." : "אלו המקשים שהגדרתם, ותוכלו לשנות אותם בכל עת בהגדרות.",
            defaultKeys ? "These are the default keys. You can change them at any time in Settings." : "These are your current keys. You can change them at any time in Settings.");

        Section("התוכנה פועלת בצורה הבאה:", "How the app works:");
        Paragraph("אם סימנתם טקסט, הפעולה תשנה רק את הטקסט המסומן.\nאם לא סימנתם, היא תפעל על השורה הנוכחית או על כל הטקסט בשדה — לפי בחירתכם בהגדרות.", "If you selected text, only that selection will change.\nWithout a selection, the action applies to the current line or all text in the field — according to your choice in Settings.");
        Section("חזרה אוטומטית לעברית", "Automatic return to Hebrew");
        var timerText = settings.AutoReturnEnabled
            ? ui.T($"עברתם לכתוב באנגלית? התוכנה תחזיר את המקלדת לעברית לאחר {settings.AutoReturnSeconds} שניות ללא הקלדה. " + (settings.AutoReturnSeconds == 30 ? "האפשרות פעילה כברירת מחדל; " : "") + "תוכלו לשנות את זמן ההמתנה או לכבות אותה בהגדרות.", $"Writing in English? The app returns the keyboard to Hebrew after {settings.AutoReturnSeconds} seconds without typing. Change the wait time or turn it off in Settings.")
            : ui.T("החזרה האוטומטית לעברית כבויה כרגע. תוכלו להפעיל אותה ולשנות את זמן ההמתנה בהגדרות.", "Automatic return to Hebrew is currently off. Enable it and change the wait time in Settings.");
        Add(ui.Paragraph(timerText));
        Section("תמיד זמינה ליד השעון", "Always available near the clock");
        Paragraph("התוכנה ממשיכה לפעול ברקע גם כשחלון ההגדרות סגור. כדי לפתוח את ההגדרות או לצפות שוב בהסבר, לחצו לחיצה ימנית על סמל התוכנה ליד השעון.", "The app keeps running in the background when Settings is closed. Right-click its icon near the clock to open Settings or read this explanation again.");
        scroll.Controls.Add(content);
        root.Controls.Add(scroll, 0, 0);

        var footer = Ui.Table(1, 2);
        footer.Padding = new Padding(28, 8, 28, 12);
        footer.BackColor = Ui.Sidebar;
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var hide = ui.Check(ui.T("לא להציג הסבר זה בהפעלה הבאה", "Do not show this explanation on the next launch"), !settings.ShowWelcome);
        hide.Margin = new Padding(0, 4, 0, 4);
        hide.CheckedChanged += (_, _) => DoNotShowAgain = hide.Checked;
        DoNotShowAgain = hide.Checked;
        footer.Controls.Add(hide, 0, 0);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = ui.Hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            RightToLeft = RightToLeft.No, WrapContents = false, Margin = Padding.Empty
        };
        var done = ui.Button(ui.T("הבנתי", "Got it"), true);
        done.Click += (_, _) => Close();
        var open = ui.Button(ui.T("פתיחת ההגדרות", "Open settings"));
        open.Width = 155;
        open.Click += (_, _) => { OpenSettings = true; Close(); };
        buttons.Controls.AddRange([done, open]);
        footer.Controls.Add(buttons, 0, 1);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);
        AcceptButton = done;
    }
}
