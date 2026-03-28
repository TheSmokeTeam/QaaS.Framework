namespace QaaS.Framework.Infrastructure;

/// <summary>
/// Extensions related to DateTime
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// converts the given time to utc based on the timezone offset in summer time given
    /// </summary>
    /// <param name="timeToConvertToUtc"> the datetime to convert to utc </param>
    /// <param name="insertionTimeTimeZoneOffsetSummerTime"> The timezone offset during summer time
    /// (for example if local summer time is gmt + 3 this value is 3)</param>
    /// <param name="isDayLightSavingTime"> True if its day light saving time right now in the configured time zone,
    /// if no value is given gets the time zone info from the system </param>
    /// <param name="timeZoneId">The time zone id used when daylight-saving resolution is needed.
    /// Defaults to <see cref="TimeZoneInfoResolver.DefaultTimeZoneId"/>.</param>
    /// <returns> The given datetime as utc </returns>
    public static DateTime ConvertDateTimeToUtcByTimeZoneOffset(
        this DateTime timeToConvertToUtc,
        int insertionTimeTimeZoneOffsetSummerTime,
        bool? isDayLightSavingTime = null,
        string? timeZoneId = null)
    {
        isDayLightSavingTime ??= IsDayLightSavingTimeInGivenDateTime(timeToConvertToUtc, timeZoneId);
        var dateTimeConvertedToUtc = 
            timeToConvertToUtc - TimeSpan.FromHours(insertionTimeTimeZoneOffsetSummerTime);
            
        if (insertionTimeTimeZoneOffsetSummerTime != 0 && !isDayLightSavingTime.Value)
            dateTimeConvertedToUtc += TimeSpan.FromHours(1);

        return DateTime.SpecifyKind(dateTimeConvertedToUtc, DateTimeKind.Utc);
    }
    
    /// <summary>
    /// adds a timezone offset to the given utc time based on the timezone offset in summer time given
    /// </summary>
    /// <param name="utcTimeToConvert"> the utc datetime to convert </param>
    /// <param name="timeZoneOffsetSummerTime"> The timezone offset during summer time
    /// (for example if local summer time is gmt + 3 this value is 3)</param>
    /// <param name="isDayLightSavingTime"> True if its day light saving time right now in the configured time zone,
    /// if no value is given gets the time zone info from the system </param>
    /// <param name="timeZoneId">The time zone id used when daylight-saving resolution is needed.
    /// Defaults to <see cref="TimeZoneInfoResolver.DefaultTimeZoneId"/>.</param>
    /// <returns> The given datetime with the added timezone offset </returns>
    public static DateTime ConvertDateTimeFromUtcToTimeZoneByTimeZoneOffset(
        this DateTime utcTimeToConvert,
        int timeZoneOffsetSummerTime,
        bool? isDayLightSavingTime = null,
        string? timeZoneId = null)
    {
        isDayLightSavingTime ??= IsDayLightSavingTimeInGivenDateTime(utcTimeToConvert, timeZoneId);
        var dateTimeWithTimeZoneOffset = 
            utcTimeToConvert + TimeSpan.FromHours(timeZoneOffsetSummerTime);
            
        if (timeZoneOffsetSummerTime != 0 && !isDayLightSavingTime.Value)
            dateTimeWithTimeZoneOffset -= TimeSpan.FromHours(1);

        return DateTime.SpecifyKind(dateTimeWithTimeZoneOffset, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Checks if its currently day light saving time in the configured time zone
    /// </summary>
    private static bool IsDayLightSavingTimeInGivenDateTime(this DateTime dateTime, string? timeZoneId = null) =>
        TimeZoneInfoResolver.ResolveTimeZoneInfo(timeZoneId).IsDaylightSavingTime(dateTime);
}
