using System;
using System.Threading;
using System.Windows.Forms;

namespace TakenLi.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, @"Local\TakenLi.Desktop", out var created);
        if (!created) return;
        ApplicationConfiguration.Initialize();
        try
        {
            var settings = SettingsStore.Load();
            Application.Run(new TrayContext(settings));
        }
        catch (Exception error)
        {
            var ui = new Ui("he");
            MessageBox.Show(ui.T("לא ניתן להפעיל את התוכנה. ההגדרות הקיימות לא נמחקו. בדוק את הקובץ %LOCALAPPDATA%\\TakenLi\\settings.json ואת הרשאות הגישה.\n", "The app could not start. Existing settings were not removed. Check %LOCALAPPDATA%\\TakenLi\\settings.json and file permissions.\n") + ui.Error(error),
                "TakenLi", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
    }
}
