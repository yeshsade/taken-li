using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;
using TakenLi.Core;

namespace TakenLi.Windows;

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TakenLi");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    private static readonly string LayoutsPath = Path.Combine(DirectoryPath, "application-layouts.json");

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
        SaveJson(FilePath, settings);
    }

    internal static Dictionary<string, long> LoadLayouts(string? path = null)
    {
        path ??= LayoutsPath;
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        var layouts = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("application-layouts");
        foreach (var (application, layout) in layouts)
        {
            if (!string.IsNullOrWhiteSpace(application) && layout != 0) result[application] = layout;
        }
        return result;
    }

    internal static void SaveLayouts(IReadOnlyDictionary<string, long> layouts, string? path = null) =>
        SaveJson(path ?? LayoutsPath, layouts);

    private static void SaveJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
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
