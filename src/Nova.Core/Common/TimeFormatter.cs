namespace Nova.Core.Common;

public static class TimeFormatter
{
    public static string FormatMilliseconds(long totalMilliseconds)
    {
        if (totalMilliseconds <= 0)
            return "00:00";

        var timeSpan = TimeSpan.FromMilliseconds(totalMilliseconds);
        return FormatTimeSpan(timeSpan);
    }

    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)timeSpan.TotalHours,
                timeSpan.Minutes,
                timeSpan.Seconds);
        }

        return string.Format("{0:D2}:{1:D2}",
            timeSpan.Minutes,
            timeSpan.Seconds);
    }
}
