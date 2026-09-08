using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using GoogleDesktopWidget.Models;

namespace GoogleDesktopWidget.Services;

public class NotesService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "GoogleDesktopWidget");
    private static readonly string NotesFile = Path.Combine(FolderPath, "notes.json");

    private readonly DispatcherTimer _debounceTimer;
    public NotesData Data { get; private set; } = new();

    public event Action? OnNotesLoaded;

    public NotesService()
    {
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            SaveImmediate();
        };

        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(NotesFile))
            {
                string json = File.ReadAllText(NotesFile);
                var loaded = JsonSerializer.Deserialize<NotesData>(json);
                if (loaded != null)
                {
                    Data = loaded;
                }
            }
        }
        catch
        {
            Data = new NotesData();
        }

        OnNotesLoaded?.Invoke();
    }

    public void TriggerDebouncedSave()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void SaveImmediate()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            string json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(NotesFile, json);
        }
        catch
        {
            // Disk I/O safe fail
        }
    }

    public void AddTask(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        Data.Tasks.Insert(0, new NoteItem { Content = content.Trim() });
        TriggerDebouncedSave();
    }

    public void DeleteTask(string id)
    {
        Data.Tasks.RemoveAll(t => t.Id == id);
        TriggerDebouncedSave();
    }

    public void ToggleTask(string id)
    {
        var item = Data.Tasks.FirstOrDefault(t => t.Id == id);
        if (item != null)
        {
            item.IsCompleted = !item.IsCompleted;
            TriggerDebouncedSave();
        }
    }

    public void UpdateScratchpad(string text)
    {
        Data.ScratchpadMarkdown = text;
        TriggerDebouncedSave();
    }
}
