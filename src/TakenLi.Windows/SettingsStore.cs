using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using TakenLi.Core;

namespace TakenLi.Windows;

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TakenLi");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    internal static AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new AppSettings();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("settings");
        settings.Validate();
        return settings;
    }

    internal static void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }

    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("startup");
            key.SetValue("TakenLi", $"\"{executable}\"");
        }
        else key.DeleteValue("TakenLi", false);
    }
}
