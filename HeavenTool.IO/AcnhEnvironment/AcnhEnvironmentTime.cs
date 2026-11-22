namespace HeavenTool.IO.AcnhEnvironment;

public static class AcnhEnvironmentTime
{
    /// <summary>Wraps any hour into the [0, 24) range.</summary>
    internal static float WrapHour(float hour) => (hour % 24 + 24) % 24;

    /// <summary>How far the date is between solstices: 1 at the northern summer solstice, -1 at the winter one.</summary>
    public static float SeasonalOffset(DateTime date, bool southernHemisphere = false)
    {
        // Measured on a non-leap year, where February 29 counts as the 28th.
        static int DayOfYear(int month, int day) => new DateOnly(2001, month, month == 2 ? Math.Min(day, 28) : day).DayOfYear;

        int today = DayOfYear(date.Month, date.Day);
        int summer = DayOfYear(6, 21);
        int winter = DayOfYear(12, 21);

        float value;
        if (today < summer) value = (float)(366 - winter + today - 1) / (366 - winter + summer - 1) * 2 - 1;
        else if (today < winter) value = (float)(today - summer) / (winter - summer) * -2 + 1;
        else value = 1 - 2 * (1 - (float)(today - winter) / (summer - winter + 365));

        return southernHemisphere ? -value : value;
    }

    /// <summary>
    /// How far through the year the date is, (day − 1) / 365 on a non-leap calendar; the southern hemisphere is half a year
    /// ahead.
    /// </summary>
    public static float YearFraction(DateTime date, bool southernHemisphere = false)
    {
        float value = (new DateOnly(2001, date.Month, date.Month == 2 ? Math.Min(date.Day, 28) : date.Day).DayOfYear - 1) / 365f;
        return southernHemisphere ? value < .5f ? value + .5f : value - .5f : value;
    }

    /// <summary>The palette hour of a clock hour: the season moves dawn and dusk, while noon stays put.</summary>
    public static float EffectiveHour(float clockHour, float seasonalOffset)
    {
        if (!float.IsFinite(clockHour)) throw new ArgumentOutOfRangeException(nameof(clockHour));
        if (!float.IsFinite(seasonalOffset) || seasonalOffset is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(seasonalOffset));

        float hour = WrapHour(clockHour);
        float dawn = seasonalOffset <= 0 ? 4 - seasonalOffset : 4 - .79999995f * seasonalOffset;
        float dusk = 19 + seasonalOffset;

        if (hour <= dawn) return Remap(hour, 0, dawn, 0, 4);
        if (hour <= 12) return Remap(hour, dawn, 12, 4, 12);
        if (hour <= dusk) return Remap(hour, 12, dusk, 12, 19);
        return Remap(hour, dusk, 24, 19, 24);
    }

    private static float Remap(float value, float from, float to, float targetFrom, float targetTo) => (value - from) / (to - from) * (targetTo - targetFrom) + targetFrom;
}
