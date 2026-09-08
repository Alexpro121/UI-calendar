namespace GoogleDesktopWidget.Models;

public class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class NotesData
{
    public string ScratchpadMarkdown { get; set; } = string.Empty;
    public List<NoteItem> Tasks { get; set; } = new();
}
