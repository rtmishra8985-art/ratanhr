using System.Globalization;

namespace HRMS.Application.Common;

/// <summary>
/// Safe ISO-8601 DateOnly parsing utilities.
/// All methods use <see cref="DateTimeStyles.None"/> with <see cref="CultureInfo.InvariantCulture"/>
/// and return validation error strings instead of throwing unhandled exceptions.
/// </summary>
public static class DateOnlyParser
{
    private const string Iso8601 = "yyyy-MM-dd";

    /// <summary>
    /// Tries to parse <paramref name="value"/> as an ISO-8601 date (yyyy-MM-dd).
    /// Returns null when <paramref name="value"/> is null or whitespace.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown with a user-friendly message when the value is non-empty but not a valid ISO-8601 date.
    /// </exception>
    public static DateOnly? ParseNullable(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateOnly.TryParseExact(value, Iso8601,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        throw new ArgumentException(
            $"'{fieldName}' has an invalid date format '{value}'. Expected yyyy-MM-dd (ISO 8601).");
    }

    /// <summary>
    /// Tries to parse <paramref name="value"/> as an ISO-8601 date.
    /// Returns (true, date) on success, (false, default) on failure or empty input.
    /// Never throws.
    /// </summary>
    public static (bool ok, DateOnly date) TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (false, default);
        var ok = DateOnly.TryParseExact(value, Iso8601,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
        return (ok, result);
    }

    /// <summary>
    /// Parse a required ISO-8601 date. Throws <see cref="ArgumentException"/> on failure.
    /// </summary>
    public static DateOnly ParseRequired(string value, string fieldName)
    {
        if (DateOnly.TryParseExact(value, Iso8601,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        throw new ArgumentException(
            $"'{fieldName}' has an invalid date format '{value}'. Expected yyyy-MM-dd (ISO 8601).");
    }
}
