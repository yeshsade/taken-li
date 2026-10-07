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
        ClientSize = new Size(660, 580);
        MinimumSize = new Size(660, 580);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        Font = new Font("Segoe UI", 10);
        BackColor = Ui.Surface;
        RightToLeft = ui.Direction;
        RightToLeftLayout = ui.Hebrew;
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(25) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var row = 0;
        void Add(Control control) { content.RowStyles.Add(new RowStyle(SizeType.AutoSize)); content.Controls.Add(control, 0, row++); }
        Add(ui.Label(ui.T("תיקון טקסט, במקום שבו אתה כותב", "Correct text where you type"), true, 40));
        Add(ui.Label(ui.T("סמן טקסט ולחץ על המקש של הפעולה. בלי סימון, הפעולה תחול על השורה או על כל השדה, לפי ההגדרות.", "Select text and press the action key. Without a selection, the current line or whole field is used, according to your settings."), height: 70));
        foreach (var action in Enum.GetValues<TextAction>())
            Add(ui.Label(ui.ActionName(action) + " — \u2066" + settings.KeyFor(action).Display + "\u2069", height: 32));
        var timerText = settings.AutoReturnEnabled
            ? ui.T($"המקלדת חוזרת לעברית אחרי {settings.AutoReturnSeconds} שניות ללא הקלדה באנגלית. כל הקלדה מתחילה את הספירה מחדש.", $"The keyboard returns to Hebrew after {settings.AutoReturnSeconds} seconds without typing in English. Each keystroke restarts the timer.")
            : ui.T("החזרה האוטומטית לעברית כבויה כרגע.", "Automatic return to Hebrew is currently off.");
        Add(ui.Label(timerText + " " + ui.T("אפשר לשנות או לכבות זאת בהגדרות. לא נוספות פריסות מקלדת.", "Change or disable it in Settings. No keyboard layouts are added."), height: 85));
        Add(ui.Label(ui.T("הגדרות והסבר זה זמינים תמיד מהסמל ליד השעון.", "Settings and this explanation are always available from the tray icon."), height: 35));
        var hide = ui.Check(ui.T("אל תציג שוב", "Do not show again"), !settings.ShowWelcome);
        hide.CheckedChanged += (_, _) => DoNotShowAgain = hide.Checked;
        DoNotShowAgain = hide.Checked;
        Add(hide);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, RightToLeft = RightToLeft.No };
        var done = ui.Button(ui.T("הבנתי", "Got it"), true);
        done.Click += (_, _) => Close();
        var open = ui.Button(ui.T("פתיחת הגדרות", "Open settings"));
        open.Click += (_, _) => { OpenSettings = true; Close(); };
        buttons.Controls.AddRange([done, open]);
        Add(buttons);
        Controls.Add(content);
        AcceptButton = done;
    }
}
