namespace MyStickies.Models;

/// <summary>"3분 전" 형식의 상대 시각 문자열</summary>
public static class RelativeTime
{
    public static string Format(DateTime value, DateTime now)
    {
        var span = now - value;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        if (span.TotalMinutes < 1) return "방금";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}분 전";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}시간 전";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}일 전";
        return value.ToString("yyyy-MM-dd");
    }
}
