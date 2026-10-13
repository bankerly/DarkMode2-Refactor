namespace DarkMode.Core.Utils;

/// <summary>
/// Sunrise/sunset for a local date using the NOAA sunrise equation.
/// Nothing leaves the machine: the result is derived from latitude, longitude and the
/// current UTC offset of the local time zone.
/// </summary>
public static class SunCalculator
{
    // 90°50' -> sun's zenith for sunrise/sunset (accounts for refraction + solar disc radius)
    private const double Zenith = 90.833;

    /// <summary>
    /// Local sunrise and sunset for <paramref name="date"/>.
    /// Returns null on polar day/night, when the sun does not cross the horizon.
    /// </summary>
    public static (DateTime Sunrise, DateTime Sunset)? TryCalculate(DateTime date, double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsNaN(longitude)) return null;
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;

        var sunriseUtc = ComputeUtcTime(date, latitude, longitude, sunrise: true);
        var sunsetUtc = ComputeUtcTime(date, latitude, longitude, sunrise: false);
        if (sunriseUtc is null || sunsetUtc is null) return null;

        // The computed times are in UTC and can belong to the previous UTC day: for an
        // eastern longitude (Beijing, UTC+8) sunrise is 22:20 UTC on the day *before* the
        // local date. Adding the offset can therefore push the value to the next local
        // day, which made "now < sunrise" true for the whole day.
        var offset = TimeZoneInfo.Local.GetUtcOffset(date);
        var sunrise = SnapToLocalDate(date.Date + sunriseUtc.Value + offset, date.Date);
        var sunset = SnapToLocalDate(date.Date + sunsetUtc.Value + offset, date.Date);

        return (sunrise, sunset);
    }

    private static DateTime SnapToLocalDate(DateTime value, DateTime localDate)
    {
        if (value < localDate) return value.AddDays(1);
        if (value >= localDate.AddDays(1)) return value.AddDays(-1);
        return value;
    }

    private static TimeSpan? ComputeUtcTime(DateTime date, double latitude, double longitude, bool sunrise)
    {
        var dayOfYear = date.DayOfYear;
        var lngHour = longitude / 15.0;

        // Sun's mean anomaly
        var t = dayOfYear + ((sunrise ? 6.0 : 18.0) - lngHour) / 24.0;
        var m = 0.9856 * t - 3.289;

        // Sun's true longitude
        var l = Normalize(m + 1.916 * Sin(m) + 0.020 * Sin(2 * m) + 282.634, 360);

        // Sun's right ascension, converted to hours
        var ra = Normalize(Atan(0.91764 * Tan(l)), 360);
        ra = (ra + (Math.Floor(l / 90.0) * 90.0 - Math.Floor(ra / 90.0) * 90.0)) / 15.0;

        // Sun's declination
        var sinDec = 0.39782 * Sin(l);
        var cosDec = Cos(Asin(sinDec));

        // Local hour angle; outside [-1, 1] the sun stays above/below the horizon all day
        var cosH = (Cos(Zenith) - sinDec * Sin(latitude)) / (cosDec * Cos(latitude));
        if (cosH is > 1.0 or < -1.0) return null;

        var h = sunrise ? 360.0 - Acos(cosH) : Acos(cosH);
        h /= 15.0;

        var localMeanTime = h + ra - 0.06571 * t - 6.622;
        return TimeSpan.FromHours(Normalize(localMeanTime - lngHour, 24));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double Sin(double degrees) => Math.Sin(ToRadians(degrees));
    private static double Cos(double degrees) => Math.Cos(ToRadians(degrees));
    private static double Tan(double degrees) => Math.Tan(ToRadians(degrees));
    private static double Asin(double value) => Math.Asin(value) * 180.0 / Math.PI;
    private static double Acos(double value) => Math.Acos(value) * 180.0 / Math.PI;
    private static double Atan(double value) => Math.Atan(value) * 180.0 / Math.PI;

    private static double Normalize(double value, double range)
    {
        value %= range;
        return value < 0 ? value + range : value;
    }
}
