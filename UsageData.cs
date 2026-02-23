namespace ClaudeUsageMonitor;

/// <summary>
/// Datenmodell für GET https://api.anthropic.com/api/oauth/usage
/// 
/// Response:
/// {
///   "five_hour":  { "utilization": 42.5, "resets_at": "2025-02-17T18:00:00Z" },
///   "seven_day":  { "utilization": 13.0, "resets_at": "2025-02-19T07:00:00Z" },
///   "extra_usage": { "is_enabled": true, "monthly_limit": 5000, "used_credits": 1250, "utilization": 25.0 }
/// }
/// </summary>
public sealed class UsageData
{
    public double SessionPercent { get; set; }
    public DateTime? SessionResetsAt { get; set; }

    public double WeeklyPercent { get; set; }
    public DateTime? WeeklyResetsAt { get; set; }
    public bool HasWeekly { get; set; }

    // Extra Usage (Pay-as-you-go Overage)
    public bool ExtraEnabled { get; set; }
    public double ExtraPercent { get; set; }
    public decimal ExtraUsedDollars { get; set; }
    public decimal ExtraLimitDollars { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.Now;

    // --- Berechnete Properties ---

    public TimeSpan SessionResetIn => TimeUntil(SessionResetsAt);
    public TimeSpan WeeklyResetIn => TimeUntil(WeeklyResetsAt);

    public string SessionResetText => FormatSpan(SessionResetIn);
    public string WeeklyResetText => FormatSpan(WeeklyResetIn);

    public string TooltipText
    {
        get
        {
            var s = $"Session: {SessionPercent:0}% | Reset: {SessionResetText}";
            if (HasWeekly) s += $"\nWeekly: {WeeklyPercent:0}% | Reset: {WeeklyResetText}";
            if (ExtraEnabled) s += $"\nExtra: ${ExtraUsedDollars:F2}/${ExtraLimitDollars:F2}";
            s += $"\nUpdated: {FetchedAt:HH:mm:ss}";
            return s.Length > 127 ? s[..127] : s;
        }
    }

    private static TimeSpan TimeUntil(DateTime? utc)
    {
        if (!utc.HasValue) return TimeSpan.Zero;
        var d = utc.Value.ToLocalTime() - DateTime.Now;
        return d > TimeSpan.Zero ? d : TimeSpan.Zero;
    }

    private static string FormatSpan(TimeSpan ts)
    {
        if (ts <= TimeSpan.Zero) return "--:--";
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}
