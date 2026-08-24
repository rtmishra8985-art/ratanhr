using HRMS.Application.Common;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for the new DateOnlyParser utility — verifies safe ISO-8601 parsing
/// replaces all former DateOnly.Parse() calls.
/// </summary>
public class DateOnlyParserTests
{
    // ── ParseNullable ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-07-18", 2026, 7, 18)]
    [InlineData("2000-01-01", 2000, 1, 1)]
    [InlineData("1990-12-31", 1990, 12, 31)]
    [InlineData("2024-02-29", 2024, 2, 29)]  // leap day
    public void ParseNullable_ValidIso8601_ReturnsCorrectDate(string input, int year, int month, int day)
    {
        var result = DateOnlyParser.ParseNullable(input, "TestField");
        Assert.NotNull(result);
        Assert.Equal(year, result!.Value.Year);
        Assert.Equal(month, result.Value.Month);
        Assert.Equal(day, result.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseNullable_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = DateOnlyParser.ParseNullable(input, "Field");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("18/07/2026")]       // dd/MM/yyyy — wrong format
    [InlineData("07-18-2026")]       // MM-dd-yyyy — wrong format
    [InlineData("2026/07/18")]       // slashes not dashes
    [InlineData("20260718")]         // no separators
    [InlineData("2026-7-18")]        // single digit month
    [InlineData("2026-13-01")]       // month 13
    [InlineData("2025-02-29")]       // not a leap year
    [InlineData("abc")]
    [InlineData("2026-00-00")]
    public void ParseNullable_InvalidFormat_ThrowsArgumentException(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DateOnlyParser.ParseNullable(input, "DateField"));
        Assert.Contains("DateField", ex.Message);
        Assert.Contains("yyyy-MM-dd", ex.Message);
    }

    // ── TryParse ─────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_ValidDate_ReturnsTrueAndCorrectDate()
    {
        var (ok, date) = DateOnlyParser.TryParse("2026-07-18");
        Assert.True(ok);
        Assert.Equal(new DateOnly(2026, 7, 18), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad-date")]
    [InlineData("2026/07/18")]
    public void TryParse_InvalidOrEmpty_ReturnsFalse(string? input)
    {
        var (ok, _) = DateOnlyParser.TryParse(input);
        Assert.False(ok);
    }

    // ── ParseRequired ─────────────────────────────────────────────────────

    [Fact]
    public void ParseRequired_ValidDate_ReturnsDate()
    {
        var result = DateOnlyParser.ParseRequired("2026-01-15", "StartDate");
        Assert.Equal(new DateOnly(2026, 1, 15), result);
    }

    [Fact]
    public void ParseRequired_InvalidDate_ThrowsWithFieldName()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DateOnlyParser.ParseRequired("not-a-date", "MyDateField"));
        Assert.Contains("MyDateField", ex.Message);
    }

    // ── Culture-invariance ────────────────────────────────────────────────

    [Fact]
    public void ParseNullable_CultureInvariant_IgnoresSystemCulture()
    {
        // Even if system culture uses different separators, parsing must be consistent.
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture =
            new System.Globalization.CultureInfo("de-DE");

        try
        {
            var result = DateOnlyParser.ParseNullable("2026-07-18", "TestDate");
            Assert.NotNull(result);
            Assert.Equal(new DateOnly(2026, 7, 18), result!.Value);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = saved;
        }
    }
}
