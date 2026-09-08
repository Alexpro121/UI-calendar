using System.IO;
using System.Text.Json;
using GoogleDesktopWidget.Models;
using Microsoft.Win32;

namespace GoogleDesktopWidget.Services;

public class SettingsService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "GoogleDesktopWidget");
    private static readonly string SettingsFile = Path.Combine(FolderPath, "settings.json");
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "GoogleDesktopWidget";

    public WidgetSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<WidgetSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch
        {
            Settings = new WidgetSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
            ApplyAutoStart(Settings.AutoStart);
        }
        catch
        {
            // Silently ignore or trace in production
        }
    }

    public void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            if (enable)
            {
                key.SetValue(AppRegistryName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(AppRegistryName) != null)
                {
                    key.DeleteValue(AppRegistryName);
                }
            }
        }
        catch
        {
            // Registry write permissions fallback
        }
    }
}SettingsService
