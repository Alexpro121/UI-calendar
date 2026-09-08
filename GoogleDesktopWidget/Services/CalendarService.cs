using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using GoogleDesktopWidget.Models;

namespace GoogleDesktopWidget.Services;

public class CalendarService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "GoogleDesktopWidget");
    private static readonly string CacheFile = Path.Combine(FolderPath, "calendar_cache.json");

    private readonly HttpClient _httpClient = new();
    private readonly System.Timers.Timer _refreshTimer = new();
    private string _currentUrl = string.Empty;

    public List<CalendarEvent> Events { get; private set; } = new();
    public event Action? OnEventsUpdated;
    public event Action<bool>? OnSyncStateChanged;

    public CalendarService()
    {
        _refreshTimer.Elapsed += async (s, e) => await RefreshCalendarAsync();
        LoadFromCache();
    }

    public void StartTimer(string icalUrl, int intervalMinutes)
    {
        _currentUrl = icalUrl;
        _refreshTimer.Stop();
        if (intervalMinutes < 1) intervalMinutes = 15;
        _refreshTimer.Interval = TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds;
        _refreshTimer.Start();
        
        _ = RefreshCalendarAsync();
    }

    public async Task RefreshCalendarAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUrl))
        {
            UpdateRelativeStatuses();
            OnEventsUpdated?.Invoke();
            return;
        }

        OnSyncStateChanged?.Invoke(true);

        try
        {
            string icsContent = await _httpClient.GetStringAsync(_currentUrl);
            var parsed = ParseIcs(icsContent);

            Events = parsed.OrderBy(e => e.StartTime).ToList();
            SaveToCache();
        }
        catch
        {
            // If offline, reload from cache
            LoadFromCache();
        }
        finally
        {
            UpdateRelativeStatuses();
            OnSyncStateChanged?.Invoke(false);
            OnEventsUpdated?.Invoke();
        }
    }

    public void UpdateRelativeStatuses()
    {
        DateTime now = DateTime.Now;
        DateTime today = now.Date;
        DateTime tomorrow = today.AddDays(1);

        foreach (var ev in Events)
        {
            ev.IsActiveNow = (now >= ev.StartTime && now <= ev.EndTime);

            if (ev.IsActiveNow)
            {
                ev.RelativeTimeStatus = "Happening Now";
            }
            else if (ev.StartTime > now && ev.StartTime <= now.AddMinutes(60))
            {
                int mins = (int)(ev.StartTime - now).TotalMinutes;
                ev.RelativeTimeStatus = $"In {mins} min{(mins == 1 ? "" : "s")}";
            }
            else if (ev.StartTime.Date == today)
            {
                ev.RelativeTimeStatus = ev.IsAllDay ? "All Day" : ev.StartTime.ToString("HH:mm");
            }
            else
            {
                ev.RelativeTimeStatus = ev.StartTime.ToString("MMM dd, HH:mm");
            }

            // Display time range
            ev.DisplayTimeRange = ev.IsAllDay 
                ? "All Day" 
                : $"{ev.StartTime:HH:mm} - {ev.EndTime:HH:mm}";

            // Group assignment
            if (ev.StartTime.Date == today)
            {
                ev.GroupKey = "Today";
            }
            else if (ev.StartTime.Date == tomorrow)
            {
                ev.GroupKey = "Tomorrow";
            }
            else
            {
                ev.GroupKey = "Upcoming";
            }
        }
    }

    private List<CalendarEvent> ParseIcs(string icsRaw)
    {
        var result = new List<CalendarEvent>();
        string unfolded = UnfoldIcsLines(icsRaw);
        var lines = unfolded.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        bool inEvent = false;
        CalendarEvent current = new();
        string rrule = string.Empty;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                inEvent = true;
                current = new CalendarEvent();
                rrule = string.Empty;
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (inEvent)
                {
                    inEvent = false;
                    ExpandEventAndRecurrences(current, rrule, result);
                }
                continue;
            }

            if (!inEvent) continue;

            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0) continue;

            string keyPart = line[..colonIndex];
            string valPart = line[(colonIndex + 1)..];

            string propName = keyPart;
            int semiColonIndex = keyPart.IndexOf(';');
            if (semiColonIndex > 0)
            {
                propName = keyPart[..semiColonIndex];
            }

            switch (propName.ToUpperInvariant())
            {
                case "UID":
                    current.Uid = valPart;
                    break;
                case "SUMMARY":
                    current.Title = UnescapeIcs(valPart);
                    break;
                case "DESCRIPTION":
                    current.Description = UnescapeIcs(valPart);
                    break;
                case "LOCATION":
                    current.Location = UnescapeIcs(valPart);
                    break;
                case "DTSTART":
                    current.StartTime = ParseIcsDateTime(keyPart, valPart, out bool isStartAllDay);
                    current.IsAllDay = isStartAllDay;
                    break;
                case "DTEND":
                    current.EndTime = ParseIcsDateTime(keyPart, valPart, out _);
                    break;
                case "RRULE":
                    rrule = valPart;
                    break;
            }
        }

        return result;
    }

    private static void ExpandEventAndRecurrences(CalendarEvent baseEvent, string rrule, List<CalendarEvent> targetList)
    {
        if (baseEvent.EndTime < baseEvent.StartTime)
        {
            baseEvent.EndTime = baseEvent.IsAllDay 
                ? baseEvent.StartTime.AddDays(1) 
                : baseEvent.StartTime.AddHours(1);
        }

        DateTime horizonLimit = DateTime.Now.AddDays(14);
        DateTime horizonStart = DateTime.Now.AddDays(-1);

        if (string.IsNullOrWhiteSpace(rrule))
        {
            if (baseEvent.EndTime >= horizonStart && baseEvent.StartTime <= horizonLimit)
            {
                targetList.Add(baseEvent);
            }
            return;
        }

        // Basic Recurrence expansion for Daily/Weekly
        TimeSpan duration = baseEvent.EndTime - baseEvent.StartTime;
        var rruleUpper = rrule.ToUpperInvariant();

        if (rruleUpper.Contains("FREQ=DAILY"))
        {
            for (int i = 0; i < 14; i++)
            {
                DateTime nextStart = baseEvent.StartTime.Date.AddDays(i) 
                    + baseEvent.StartTime.TimeOfDay;
                DateTime nextEnd = nextStart + duration;

                if (nextEnd >= horizonStart && nextStart <= horizonLimit)
                {
                    targetList.Add(new CalendarEvent
                    {
                        Uid = $"{baseEvent.Uid}_{i}",
                        Title = baseEvent.Title,
                        Description = baseEvent.Description,
                        Location = baseEvent.Location,
                        StartTime = nextStart,
                        EndTime = nextEnd,
                        IsAllDay = baseEvent.IsAllDay
                    });
                }
            }
        }
        else if (rruleUpper.Contains("FREQ=WEEKLY"))
        {
            for (int week = 0; week < 4; week++)
            {
                DateTime nextStart = baseEvent.StartTime.AddDays(week * 7);
                DateTime nextEnd = nextStart + duration;

                if (nextEnd >= horizonStart && nextStart <= horizonLimit)
                {
                    targetList.Add(new CalendarEvent
                    {
                        Uid = $"{baseEvent.Uid}_{week}",
                        Title = baseEvent.Title,
                        Description = baseEvent.Description,
                        Location = baseEvent.Location,
                        StartTime = nextStart,
                        EndTime = nextEnd,
                        IsAllDay = baseEvent.IsAllDay
                    });
                }
            }
        }
        else
        {
            if (baseEvent.EndTime >= horizonStart && baseEvent.StartTime <= horizonLimit)
            {
                targetList.Add(baseEvent);
            }
        }
    }

    private static string UnfoldIcsLines(string ics)
    {
        return Regex.Replace(ics, @"\r?\n[ \t]", "");
    }

    private static string UnescapeIcs(string text)
    {
        return text.Replace("\\n", "\n")
                   .Replace("\\N", "\n")
                   .Replace("\\,", ",")
                   .Replace("\\;", ";")
                   .Replace("\\\\", "\\");
    }

    private static DateTime ParseIcsDateTime(string key, string val, out bool isAllDay)
    {
        isAllDay = key.Contains("VALUE=DATE") || val.Length == 8;

        if (isAllDay)
        {
            if (DateTime.TryParseExact(val.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        // e.g. 20260908T153000Z or 20260908T153000
        string cleanVal = val.Trim();
        bool isUtc = cleanVal.EndsWith("Z");
        if (isUtc) cleanVal = cleanVal[..^1];

        if (DateTime.TryParseExact(cleanVal, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return isUtc ? dt.ToLocalTime() : dt;
        }

        return DateTime.Now;
    }

    private void SaveToCache()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }
            string json = JsonSerializer.Serialize(Events, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CacheFile, json);
        }
        catch { }
    }

    private void LoadFromCache()
    {
        try
        {
            if (File.Exists(CacheFile))
            {
                string json = File.ReadAllText(CacheFile);
                var cached = JsonSerializer.Deserialize<List<CalendarEvent>>(json);
                if (cached != null)
                {
                    Events = cached;
                    UpdateRelativeStatuses();
                }
            }
        }
        catch { }
    }
}
