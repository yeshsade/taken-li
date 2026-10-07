using System;
using System.Drawing;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class Ui
{
    internal readonly bool Hebrew;
    internal Ui(string language) => Hebrew = language == "he";
    internal string T(string he, string en) => Hebrew ? he : en;
    internal RightToLeft Direction => Hebrew ? RightToLeft.Yes : RightToLeft.No;
    internal ContentAlignment Alignment => Hebrew ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

    internal string ActionName(TextAction action) => action switch
    {
        TextAction.FixLayout => T("תיקון עברית ואנגלית", "Fix Hebrew and English"),
        TextAction.SwapCase => T("החלפת אותיות גדולות וקטנות", "Swap uppercase and lowercase"),
        TextAction.Reverse => T("היפוך סדר האותיות", "Reverse letter order"),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    internal string Error(Exception error)
    {
        if (error.Message.StartsWith("hotkey:", StringComparison.Ordinal))
            return T("לא ניתן לרשום את המקש ", "Cannot register ") + error.Message.Split(':')[1] +
                T(". ייתכן שהוא בשימוש בתוכנה אחרת, למשל LangOver. בחר מקש אחר או סגור את התוכנה האחרת.", ". It may be used by another application, such as LangOver. Choose another key or close the other application.");
        return error.Message switch
        {
            "duplicate" => T("לכל פעולה צריך לבחור מקש שונה.", "Choose a different key for each action."),
            "typing-key" => T("אות או ספרה צריכות צירוף עם Ctrl, Shift או Alt, כדי שלא להפריע להקלדה. מקשי F יכולים לפעול לבד.", "Letters and digits require Ctrl, Shift or Alt so normal typing is not intercepted. Function keys can be used alone."),
            "key" => T("בחר מקש F, אות או ספרה.", "Choose a function key, letter or digit."),
            "delay" => T("זמן ההמתנה צריך להיות בין שנייה אחת לשעה.", "The wait time must be between one second and one hour."),
            "focus" => T("החלון הפעיל השתנה או אינו זמין. חזור לשדה הטקסט ונסה שוב.", "The active window changed or is unavailable. Return to the text field and try again."),
            "release" => T("שחרר את מקשי Ctrl, Shift ו־Alt ונסה שוב.", "Release Ctrl, Shift and Alt and try again."),
            "selection" => T("לא התקבל טקסט לעריכה. נסה לסמן את הטקסט ולהפעיל שוב. ייתכן שהתוכנה הפעילה אינה תומכת בפעולה.", "No editable text was received. Select the text and try again. The active application may not support this action."),
            "clipboard" => T("לוח ההעתקה בשימוש. המתן רגע ונסה שוב.", "The clipboard is busy. Wait a moment and try again."),
            "layout" => T("לא ניתן לעבור לשפה המבוקשת בפריסות הקיימות. התוכנה לא הוסיפה פריסה חדשה.", "The requested language cannot be selected from the existing layouts. No new layout was added."),
            "password" => T("התוכנה אינה עורכת שדות סיסמה.", "Password fields are not edited."),
            "readonly" => T("השדה הפעיל אינו ניתן לעריכה.", "The active field is not editable."),
            "input" => T("Windows לא אפשר לשלוח מקשים. הפעולה לא בהכרח נתמכת בחלון זה, במיוחד אם הוא פועל כמנהל.", "Windows blocked keyboard input. This window may not support the action, especially if it runs as administrator."),
            "hook" => T("לא ניתן לעקוב אחר הפסקות בהקלדה. סגור ופתח שוב את התוכנה.", "Typing activity could not be monitored. Restart the application."),
            _ => T("לא ניתן להשלים את הפעולה. בדוק את הרשאות הקבצים וההגדרות ונסה שוב.", "The action could not be completed. Check file permissions and settings and try again.")
        };
    }

    internal void ShowError(Exception error, IWin32Window? owner = null) => MessageBox.Show(owner, Error(error),
        T("לא ניתן להשלים את הפעולה", "Action could not be completed"), MessageBoxButtons.OK, MessageBoxIcon.Warning,
        MessageBoxDefaultButton.Button1, Hebrew ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);

    internal static readonly Color Accent = Color.FromArgb(70, 97, 81);
    internal static readonly Color Muted = Color.FromArgb(100, 107, 101);
    internal static readonly Color Border = Color.FromArgb(222, 223, 215);
    internal static readonly Color Surface = Color.FromArgb(255, 254, 251);
    internal static readonly Color Sidebar = Color.FromArgb(245, 244, 239);
    internal static readonly Color Selected = Color.FromArgb(228, 234, 226);
    internal static readonly Color KeySurface = Color.FromArgb(244, 245, 239);

    internal Label Label(string text, bool bold = false, int height = 30) => new()
    {
        Text = text, Dock = DockStyle.Top, Height = height, TextAlign = Alignment,
        RightToLeft = Direction, Font = new Font("Segoe UI", bold ? 11 : 10, bold ? FontStyle.Bold : FontStyle.Regular),
        AutoEllipsis = false, Margin = new Padding(0, 5, 0, 5)
    };

    internal Button Button(string text, bool primary = false) => new()
    {
        Text = text, AutoSize = true, MinimumSize = new Size(85, 34), Height = 34,
        BackColor = primary ? Accent : Surface, ForeColor = primary ? Color.White : Color.FromArgb(41, 45, 42),
        FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = !primary, Margin = new Padding(5),
        AccessibleName = text
    };

    internal CheckBox Check(string text, bool value) => new()
    {
        Text = text, Checked = value, AutoSize = true, RightToLeft = Direction,
        Margin = new Padding(0, 10, 0, 10), AccessibleName = text
    };
}
