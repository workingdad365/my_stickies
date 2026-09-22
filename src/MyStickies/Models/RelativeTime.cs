using MyStickies.Localization;

namespace MyStickies.Models;

/// <summary>"3분 전" 형식의 상대 시각 문자열</summary>
public static class RelativeTime
{
    public static string Format(DateTime value, DateTime now)
    {
        var span = now - value;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        if (span.TotalMinutes < 1) return Strings.Get("JustNow");
        if (span.TotalHours < 1) return Strings.Get((int)span.TotalMinutes == 1 ? "MinuteAgo" : "MinutesAgo", (int)span.TotalMinutes);
        if (span.TotalDays < 1) return Strings.Get((int)span.TotalHours == 1 ? "HourAgo" : "HoursAgo", (int)span.TotalHours);
        if (span.TotalDays < 30) return Strings.Get((int)span.TotalDays == 1 ? "DayAgo" : "DaysAgo", (int)span.TotalDays);
        return value.ToString("yyyy-MM-dd");
    }
}
