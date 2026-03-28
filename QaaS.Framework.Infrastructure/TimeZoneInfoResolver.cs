namespace QaaS.Framework.Infrastructure;

/// <summary>
/// Resolves time zone identifiers across operating systems while keeping a single default zone.
/// </summary>
public static class TimeZoneInfoResolver
{
    public const string DefaultTimeZoneId = "Asia/Jerusalem";
    public const string DefaultWindowsTimeZoneId = "Israel Standard Time";

    /// <summary>
    /// Resolves the requested time zone id to a <see cref="TimeZoneInfo"/> on the current platform.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZoneInfo(string? timeZoneId = null)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            if (TryResolve(timeZoneId, out var explicitTimeZoneInfo))
                return explicitTimeZoneInfo;

            throw new TimeZoneNotFoundException($"Could not resolve time zone '{timeZoneId}'.");
        }

        foreach (var defaultTimeZoneCandidate in GetDefaultTimeZoneCandidates())
        {
            if (TryResolve(defaultTimeZoneCandidate, out var timeZoneInfo))
                return timeZoneInfo;
        }

        throw new TimeZoneNotFoundException(
            $"Could not resolve default time zone '{DefaultTimeZoneId}' or its Windows fallback '{DefaultWindowsTimeZoneId}'.");
    }

    private static IEnumerable<string> GetDefaultTimeZoneCandidates()
    {
        yield return DefaultTimeZoneId;

        if (!string.Equals(DefaultTimeZoneId, DefaultWindowsTimeZoneId, StringComparison.Ordinal))
            yield return DefaultWindowsTimeZoneId;
    }

    private static bool TryResolve(string timeZoneId, out TimeZoneInfo timeZoneInfo)
    {
        if (TryFindSystemTimeZone(timeZoneId, out timeZoneInfo))
            return true;

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsTimeZoneId) &&
            TryFindSystemTimeZone(windowsTimeZoneId, out timeZoneInfo))
            return true;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaTimeZoneId) &&
            TryFindSystemTimeZone(ianaTimeZoneId, out timeZoneInfo))
            return true;

        timeZoneInfo = null!;
        return false;
    }

    private static bool TryFindSystemTimeZone(string timeZoneId, out TimeZoneInfo timeZoneInfo)
    {
        try
        {
            timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZoneInfo = null!;
            return false;
        }
    }
}
