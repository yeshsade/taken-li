using System.Drawing.Imaging;
using TakenLi.Core;
using TakenLi.Windows;

namespace TakenLi.Windows.Checks;

internal static class Program
{
    private static int _checks;
    private static string _output = null!;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        _output = args.Length > 0 ? args[0] : Path.Combine("artifacts", "ui-checks");
        Directory.CreateDirectory(_output);
        try
        {
            foreach (var language in new[] { "he", "en" })
            {
                CheckSettings(language);
                CheckWelcome(language);
                using var key = new KeyDialog(new Ui(language), new Ui(language).ActionName(TextAction.FixLayout), new Hotkey(121));
                Open(key);
                Assert(key.Result == new Hotkey(121), "Opening the key dialog preserves the shortcut");
                CheckText(key);
                Capture(key, $"key-{language}");
            }
            Assert(Contrast(Ui.Ink, Ui.Surface) >= 7, "Main text contrast >= 7:1");
            Assert(Contrast(Ui.Muted, Ui.Surface) >= 4.5, "Secondary text contrast >= 4.5:1");
            Assert(Contrast(Color.White, Ui.Accent) >= 4.5, "Primary button contrast >= 4.5:1");
            Console.WriteLine($"PASS: {_checks} native Windows UI checks; screenshots: {_output}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("::error::" + error.Message);
            Console.Error.WriteLine(error);
            foreach (Form form in Application.OpenForms) Capture(form, "failure-" + form.GetType().Name);
            return 1;
        }
    }

    private static void CheckSettings(string language)
    {
        var source = new AppSettings { Language = language };
        AppSettings? saved = null;
        using var form = new SettingsForm(source, settings => saved = settings, () => { });
        Open(form);
        foreach (var size in new[] { new Size(980, 700), new Size(854, 601) })
        {
            form.ClientSize = size;
            for (var page = 0; page < 4; page++)
            {
                Find<Button>(form, "Page" + page).PerformClick();
                Pump(form);
                var sidebar = Find<TableLayoutPanel>(form, "Sidebar");
                var pane = Find<Panel>(form, "ContentPane");
                var nav = Enumerable.Range(0, 4).Select(i => Find<Button>(form, "Page" + i)).ToArray();
                Assert(sidebar.RowCount == 5, "Four navigation rows and an empty filler row");
                Assert(nav.All(button => button.Height <= 50 * form.DeviceDpi / 96.0), "Navigation never stretches vertically");
                Assert(nav.Select(button => button.Height).Distinct().Count() == 1, "Equal navigation heights");
                Assert(language == "he" ? sidebar.Left > pane.Left : sidebar.Left < pane.Left, "Sidebar on the language-appropriate side");
                Assert(!pane.HorizontalScroll.Visible, "No horizontal content scroll");
                var heading = Find<Label>(form, "PageHeading");
                Assert(heading.TextAlign == (language == "he" ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft), "Page heading alignment");
                CheckText(form);
                foreach (var check in Descendants(pane).OfType<CheckBox>())
                {
                    Assert(check.Anchor == (language == "he" ? AnchorStyles.Right : AnchorStyles.Left), "Checkbox anchored to reading edge");
                    Assert(check.Width <= check.Parent!.ClientSize.Width, "Checkbox label fits");
                }
                if (page == 0)
                {
                    foreach (var action in Enum.GetValues<TextAction>())
                    {
                        var badge = Find<Label>(form, "Key" + action);
                        Assert(badge.RightToLeft == RightToLeft.No, "Shortcut remains LTR");
                        Assert(badge.Height <= 40 * form.DeviceDpi / 96.0, "Compact shortcut badge");
                        Assert(badge.Text == source.KeyFor(action).Display, "Displayed shortcut unchanged");
                    }
                    var labels = Descendants(pane).OfType<Label>().Select(x => x.Text).ToArray();
                    Assert(labels.Contains("akuo") && labels.Contains("שלום") && labels.Contains("hELLO") && labels.Contains("םולש"), "Examples are separate intact strings");
                }
                Capture(form, $"settings-{language}-{page}-{size.Width}");
            }
        }

        Find<Button>(form, "Page1").PerformClick();
        Pump(form);
        var enabled = Descendants(Find<Panel>(form, "ContentPane")).OfType<CheckBox>().Single();
        var delay = Descendants(form).OfType<NumericUpDown>().Single();
        enabled.Checked = false;
        Assert(!delay.Enabled, "Disabling return disables the delay field");
        enabled.Checked = true;
        delay.Value = 45;
        Find<Button>(form, "Page2").PerformClick();
        Pump(form);
        var radios = Descendants(form).OfType<RadioButton>().ToArray();
        radios.Single(r => r.Text == (language == "he" ? "כל הטקסט בשדה" : "All text in the field")).Checked = true;
        Find<Button>(form, "Page3").PerformClick();
        Pump(form);
        var checks = Descendants(Find<Panel>(form, "ContentPane")).OfType<CheckBox>().ToArray();
        checks[0].Checked = true;
        checks[1].Checked = false;
        Assert(source.AutoReturnSeconds == 30 && !source.StartWithWindows && source.ShowWelcome, "Editing does not mutate live settings");
        FindButton(form, language == "he" ? "שמירה" : "Save").PerformClick();
        Assert(saved is { AutoReturnSeconds: 45, StartWithWindows: true, ShowWelcome: false, UnselectedScope: SelectionScope.WholeField }, "Save preserves changed settings and scope");

        using var cancelled = new SettingsForm(source, _ => throw new Exception("Cancel must not save"), () => { });
        Open(cancelled);
        FindButton(cancelled, language == "he" ? "ביטול" : "Cancel").PerformClick();
        Assert(source.AutoReturnSeconds == 30, "Cancel preserves original settings");

        using var switcher = new SettingsForm(source, _ => { }, () => { });
        Open(switcher);
        Descendants(switcher).OfType<ComboBox>().Single().SelectedIndex = language == "he" ? 1 : 0;
        Pump(switcher);
        Assert(switcher.Text == (language == "he" ? "Settings" : "הגדרות"), "Changing language rebuilds the correct interface");
    }

    private static void CheckWelcome(string language)
    {
        using var form = new WelcomeForm(new AppSettings { Language = language });
        Open(form);
        CheckText(form);
        var hide = Descendants(form).OfType<CheckBox>().Single();
        Assert(hide.Visible && form.ClientRectangle.Contains(form.PointToClient(hide.PointToScreen(Point.Empty))), "Welcome preference visible without scrolling");
        hide.Checked = true;
        Assert(form.DoNotShowAgain, "Welcome checkbox retains preference");
        var labels = Descendants(form).OfType<Label>().Select(x => x.Text).ToArray();
        if (language == "he")
        {
            Assert(labels.Contains("כתבתם בטעות באנגלית במקום בעברית?!"), "Approved welcome opening");
            Assert(labels.Contains("מקש אחד והכול מסתדר."), "Approved welcome slogan");
            Assert(labels.Contains("התוכנה פועלת בצורה הבאה:"), "Approved selection section");
        }
        Capture(form, "welcome-" + language);
        FindButton(form, language == "he" ? "פתיחת ההגדרות" : "Open settings").PerformClick();
        Assert(form.OpenSettings, "Welcome opens Settings on request");

        using var customized = new WelcomeForm(new AppSettings { Language = language, AutoReturnEnabled = false, FixLayoutKey = new Hotkey(119) });
        Open(customized);
        Assert(Descendants(customized).OfType<Label>().Any(x => x.Text == "F8"), "Welcome reflects customized shortcuts");
        Assert(Descendants(customized).OfType<Label>().Any(x => x.Text.Contains(language == "he" ? "כבויה כרגע" : "currently off")), "Welcome reflects disabled timer");
    }

    private static void Open(Form form) { form.Show(); Pump(form); }
    private static void Pump(Form form) { Application.DoEvents(); form.PerformLayout(); Application.DoEvents(); }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var grandchild in Descendants(child)) yield return grandchild;
        }
    }
    private static T Find<T>(Control form, string name) where T : Control => Descendants(form).OfType<T>().Single(x => x.Name == name);
    private static Button FindButton(Control form, string text) => Descendants(form).OfType<Button>().Single(x => x.Text == text);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); _checks++; }
    private static void CheckText(Control form)
    {
        Assert(!Descendants(form).Any(x => x.Text.Any(c => c is >= '\u2066' and <= '\u2069')), "No unsupported Unicode bidi isolates in native text");
    }
    private static void Capture(Form form, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(_output, name + ".png"), ImageFormat.Png);
    }
    private static double Contrast(Color a, Color b)
    {
        static double L(Color color)
        {
            static double C(byte value) { var n = value / 255.0; return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4); }
            return 0.2126 * C(color.R) + 0.7152 * C(color.G) + 0.0722 * C(color.B);
        }
        var x = L(a); var y = L(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
