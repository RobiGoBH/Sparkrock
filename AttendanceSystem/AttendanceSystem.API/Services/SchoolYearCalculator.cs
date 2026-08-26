using System.Text.RegularExpressions;

namespace AttendanceSystem.API.Services;

// Mirrors the school-year convention duplicated in both legacy stored procedures:
// Sep-Aug academic year, e.g. a date in Sep 2025 - Aug 2026 belongs to "2025-2026".
public static partial class SchoolYearCalculator
{
    public static string GetSchoolYear(DateOnly date) =>
        date.Month >= 9
            ? $"{date.Year}-{date.Year + 1}"
            : $"{date.Year - 1}-{date.Year}";

    public static string GetCurrentSchoolYear() => GetSchoolYear(DateOnly.FromDateTime(DateTime.UtcNow));

    public static bool IsValid(string schoolYear)
    {
        var match = SchoolYearPattern().Match(schoolYear);
        if (!match.Success)
        {
            return false;
        }

        var startYear = int.Parse(match.Groups[1].Value);
        var endYear = int.Parse(match.Groups[2].Value);
        return endYear == startYear + 1;
    }

    // Returns the inclusive Sep 1 - Aug 31 calendar range backing a "YYYY-YYYY+1" school year.
    public static (DateOnly Start, DateOnly End) GetDateRange(string schoolYear)
    {
        if (!IsValid(schoolYear))
        {
            throw new ArgumentException($"'{schoolYear}' is not a valid school year (expected format 'YYYY-YYYY+1').", nameof(schoolYear));
        }

        var startYear = int.Parse(schoolYear[..4]);
        return (new DateOnly(startYear, 9, 1), new DateOnly(startYear + 1, 8, 31));
    }

    [GeneratedRegex(@"^(\d{4})-(\d{4})$")]
    private static partial Regex SchoolYearPattern();
}
