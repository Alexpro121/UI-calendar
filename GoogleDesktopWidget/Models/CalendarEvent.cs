namespace GoogleDesktopWidget.Models;

public class CalendarEvent
{
    public string Uid { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Untitled Event";
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsActiveNow { get; set; }
    public string RelativeTimeStatus { get; set; } = string.Empty;
    public string DisplayTimeRange { get; set; } = string.Empty;
    public string GroupKey { get; set; } = "Upcoming"; // "Today", "Tomorrow", "Upcoming"
    public bool IsExpanded { get; set; } = false;
}
