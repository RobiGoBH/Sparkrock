using AttendanceSystem.API.Services;
using FluentAssertions;
using Xunit;

namespace AttendanceSystem.Tests;

public class SchoolYearCalculatorTests
{
    [Theory]
    [InlineData(2025, 9, 1, "2025-2026")]
    [InlineData(2025, 12, 25, "2025-2026")]
    [InlineData(2026, 1, 15, "2025-2026")]
    [InlineData(2026, 8, 31, "2025-2026")]
    [InlineData(2026, 9, 1, "2026-2027")]
    public void GetSchoolYear_ReturnsSeptToAugustYear(int year, int month, int day, string expected)
    {
        var result = SchoolYearCalculator.GetSchoolYear(new DateOnly(year, month, day));

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2025-2026", true)]
    [InlineData("2025-2027", false)]
    [InlineData("not-a-year", false)]
    [InlineData("", false)]
    public void IsValid_ValidatesSchoolYearFormat(string schoolYear, bool expected)
    {
        SchoolYearCalculator.IsValid(schoolYear).Should().Be(expected);
    }

    [Fact]
    public void GetDateRange_ReturnsSepFirstToAug31()
    {
        var (start, end) = SchoolYearCalculator.GetDateRange("2025-2026");

        start.Should().Be(new DateOnly(2025, 9, 1));
        end.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void GetDateRange_ThrowsForInvalidSchoolYear()
    {
        var act = () => SchoolYearCalculator.GetDateRange("bogus");

        act.Should().Throw<ArgumentException>();
    }
}
