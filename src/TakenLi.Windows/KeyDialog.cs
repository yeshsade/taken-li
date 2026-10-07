using System;
using System.Drawing;
using System.Windows.Forms;
using TakenLi.Core;

namespace TakenLi.Windows;

internal sealed class KeyDialog : Form
{
    internal Hotkey Result { get; private set; }
    private readonly Label _preview;
    private readonly Ui _ui;

    internal KeyDialog(Ui ui, string actionName, Hotkey current)
    {
        _ui = ui;
        Result = current;
        Text = ui.T("בחירת מקש", "Choose a key");
        Font = new Font("Segoe UI", 10);
        BackColor = Ui.Surface;
        ClientSize = new Size(460, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        RightToLeft = ui.Direction;
        RightToLeftLayout = false;
        KeyPreview = true;
        var content = Ui.Table(1, 4);
        content.Padding = new Padding(24);
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        content.Controls.Add(ui.Label(actionName, true), 0, 0);
        content.Controls.Add(ui.Label(ui.T("לחץ על מקש F, או על אות/ספרה עם Ctrl, Shift או Alt. לאחר מכן לחץ על אישור.", "Press a function key, or a letter/digit with Ctrl, Shift or Alt. Then select OK."), height: 60), 0, 1);
        _preview = new Label { Text = current.Display, Font = new Font("Consolas", 15), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No };
        content.Controls.Add(_preview, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = ui.Hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, RightToLeft = RightToLeft.No, WrapContents = false };
        var cancel = ui.Button(ui.T("ביטול", "Cancel"));
        cancel.DialogResult = DialogResult.Cancel;
        var ok = ui.Button(ui.T("אישור", "OK"), true);
        ok.DialogResult = DialogResult.OK;
        buttons.Controls.AddRange([ok, cancel]);
        content.Controls.Add(buttons, 0, 3);
        Controls.Add(content);
        CancelButton = cancel;
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        var key = (int)(keyData & Keys.KeyCode);
        if (key is >= 112 and <= 135 or >= 65 and <= 90 or >= 48 and <= 57)
        {
            var result = new Hotkey(key, keyData.HasFlag(Keys.Control), keyData.HasFlag(Keys.Shift), keyData.HasFlag(Keys.Alt));
            if (key < 112 && result.Modifiers == 0)
            {
                _preview.Text = _ui.T("צרף Ctrl, Shift או Alt", "Add Ctrl, Shift or Alt");
            }
            else { Result = result; _preview.Text = result.Display; }
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }
}
