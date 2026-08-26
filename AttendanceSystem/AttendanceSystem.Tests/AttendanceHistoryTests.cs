using AttendanceSystem.API.Services;
using FluentAssertions;
using Xunit;

namespace AttendanceSystem.Tests;

public class AttendanceHistoryTests
{
    [Fact]
    public async Task GetAttendanceHistory_ReturnsSeededRecords_OrderedByDateDescending()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var history = await service.GetAttendanceHistoryAsync(1, "2025-2026");

        history.Should().HaveCount(3);
        history.Should().BeInDescendingOrder(h => h.AttendDate);
        history.First().AttendDate.Should().Be(new DateOnly(2025, 9, 5));
        history.First().AttendCode.Should().Be("A");
        history.First().AttendCodeDescription.Should().Be("Absent - Unexcused");
        history.First().IsAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task GetAttendanceHistory_ForYearWithNoRecords_ReturnsEmptyList()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var history = await service.GetAttendanceHistoryAsync(1, "2020-2021");

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAttendanceHistory_ForUnknownStudent_ThrowsNotFoundException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.GetAttendanceHistoryAsync(999, "2025-2026");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAttendanceHistory_WithInvalidSchoolYearFormat_ThrowsValidationException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.GetAttendanceHistoryAsync(1, "not-a-year");

        await act.Should().ThrowAsync<AttendanceValidationException>();
    }
}
