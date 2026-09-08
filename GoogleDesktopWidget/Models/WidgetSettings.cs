namespace GoogleDesktopWidget.Models;

public class WidgetSettings
{
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double Opacity { get; set; } = 0.94;
    public bool EnableAcrylic { get; set; } = true;
    public string ThemeName { get; set; } = "GoogleBlue"; // GoogleBlue, EmeraldForest, SunsetCoral, AmoledBlack
    public bool PinToDesktop { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public string CalendarUrl { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; } = 15;
}
