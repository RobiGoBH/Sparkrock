using AttendanceSystem.API.Contracts;
using AttendanceSystem.API.Models;
using AttendanceSystem.API.Services;
using FluentAssertions;
using Xunit;

namespace AttendanceSystem.Tests;

public class ChronicAbsenteeismTests
{
    [Fact]
    public void AbsenceAlertThreshold_DefaultsToTen_WhenNotExplicitlySetOnSchool()
    {
        // Mirrors the legacy ISNULL(sc.AbsenceAlertThreshold, 10) fallback: an absolute
        // integer count, not a percentage of enrolled days.
        var school = new School { SchoolId = 1, SchoolName = "Unconfigured School" };

        school.AbsenceAlertThreshold.Should().Be(10);
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_ExactlyAtThreshold_ReturnsTrue()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 5 starts with zero absences; Roosevelt Middle School's threshold is 8.
        // Legacy: `TotalAbsences >= threshold`, so landing exactly on 8 must already be chronic.
        for (var day = 1; day <= 8; day++)
        {
            await service.SaveDailyAttendanceAsync(
                new SaveDailyAttendanceRequest(2, new DateOnly(2025, 10, day), new List<StudentAttendanceEntry> { new(5, "A") }),
                "teacher2");
        }

        var status = await service.GetChronicAbsenteeismStatusAsync(5, "2025-2026");

        status.TotalAbsences.Should().Be(8);
        status.IsChronicallyAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_BelowThreshold_ReturnsFalse()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 1 is seeded with 1 absence; Lincoln Elementary's threshold is 10.
        var status = await service.GetChronicAbsenteeismStatusAsync(1, "2025-2026");

        status.TotalAbsences.Should().Be(1);
        status.Threshold.Should().Be(10);
        status.IsChronicallyAbsent.Should().BeFalse();
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_AtOrAboveThreshold_ReturnsTrue()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 4 is seeded with 9 absences; Roosevelt Middle School's threshold is 8.
        var status = await service.GetChronicAbsenteeismStatusAsync(4, "2025-2026");

        status.TotalAbsences.Should().Be(9);
        status.Threshold.Should().Be(8);
        status.IsChronicallyAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_WithNoSummaryYet_ReturnsZeroAbsencesAndNotChronic()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var status = await service.GetChronicAbsenteeismStatusAsync(2, "2025-2026");

        status.TotalAbsences.Should().Be(0);
        status.IsChronicallyAbsent.Should().BeFalse();
        status.LastUpdated.Should().BeNull();
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_DefaultsToCurrentSchoolYear_WhenNoneProvided()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var status = await service.GetChronicAbsenteeismStatusAsync(4, schoolYear: null);

        status.SchoolYear.Should().Be(SchoolYearCalculator.GetCurrentSchoolYear());
    }

    [Fact]
    public async Task GetChronicAbsenteeismStatus_ForUnknownStudent_ThrowsNotFoundException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.GetChronicAbsenteeismStatusAsync(999, "2025-2026");

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
