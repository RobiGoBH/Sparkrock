using AttendanceSystem.API.Contracts;
using AttendanceSystem.API.Models;
using AttendanceSystem.API.Services;
using FluentAssertions;
using Xunit;

namespace AttendanceSystem.Tests;

public class SaveDailyAttendanceTests
{
    [Fact]
    public async Task SaveDailyAttendance_WithLargeBatchOfStudents_UpsertsAllRecordsInOneBatch()
    {
        using var db = TestDbContextFactory.Create();

        // Extra students beyond the seeded roster, to exercise batching across a class-sized
        // save rather than the handful of seeded students.
        const int studentCount = 50;
        var extraStudentIds = Enumerable.Range(100, studentCount).ToList();
        db.Students.AddRange(extraStudentIds.Select(id => new Student
        {
            StudentId = id,
            SchoolId = 1,
            FirstName = $"Student{id}",
            LastName = "Batch",
            Active = true,
        }));
        await db.SaveChangesAsync();

        var service = new AttendanceService(db);
        var attendDate = new DateOnly(2025, 10, 21);

        // Every third student is absent; the rest present. Each save is an upsert, so run it
        // twice to also prove re-saving the same batch doesn't create duplicate rows.
        var records = extraStudentIds
            .Select(id => new StudentAttendanceEntry(id, id % 3 == 0 ? "A" : "P"))
            .ToList();

        var firstResponse = await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, attendDate, records), "teacher1");
        var secondResponse = await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, attendDate, records), "teacher1");

        firstResponse.RecordsSaved.Should().Be(studentCount);
        secondResponse.RecordsSaved.Should().Be(studentCount);

        var absentStudentIds = extraStudentIds.Where(id => id % 3 == 0).ToList();
        foreach (var studentId in absentStudentIds.Take(3))
        {
            var history = await service.GetAttendanceHistoryAsync(studentId, "2025-2026");
            history.Should().ContainSingle(h => h.AttendDate == attendDate && h.IsAbsent);

            var status = await service.GetChronicAbsenteeismStatusAsync(studentId, "2025-2026");
            status.TotalAbsences.Should().Be(1);
        }
    }

    [Fact]
    public async Task SaveDailyAttendance_ForNewDate_CreatesAttendanceRecords()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);
        var attendDate = new DateOnly(2025, 10, 1);

        var request = new SaveDailyAttendanceRequest(
            SchoolId: 1,
            AttendDate: attendDate,
            Records: new List<StudentAttendanceEntry>
            {
                new(StudentId: 1, AttendCode: "P"),
                new(StudentId: 2, AttendCode: "A", MinutesLate: 0, Notes: "Called in sick"),
            });

        var response = await service.SaveDailyAttendanceAsync(request, submittedBy: "teacher1");

        response.RecordsSaved.Should().Be(2);
        response.SchoolId.Should().Be(1);
        response.AttendDate.Should().Be(attendDate);

        var history = await service.GetAttendanceHistoryAsync(2, "2025-2026");
        history.Should().ContainSingle(h => h.AttendDate == attendDate && h.AttendCode == "A" && h.IsAbsent && h.Notes == "Called in sick");
    }

    [Fact]
    public async Task SaveDailyAttendance_CalledTwiceForSameStudentAndDate_UpdatesInPlaceInsteadOfDuplicating()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);
        var attendDate = new DateOnly(2025, 10, 2);

        await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, attendDate, new List<StudentAttendanceEntry> { new(2, "P") }),
            "teacher1");

        await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, attendDate, new List<StudentAttendanceEntry> { new(2, "T", MinutesLate: 15) }),
            "teacher1");

        var history = await service.GetAttendanceHistoryAsync(2, "2025-2026");

        history.Should().ContainSingle(h => h.AttendDate == attendDate);
        history.Single().AttendCode.Should().Be("T");
        history.Single().MinutesLate.Should().Be(15);
    }

    [Fact]
    public async Task SaveDailyAttendance_RecomputesSummary_WhenStudentCrossesAbsenceThreshold()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 4 is seeded with 9 absences already (threshold for school 2 is 8),
        // so a single additional absence should keep them flagged chronically absent.
        await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(2, new DateOnly(2025, 10, 6), new List<StudentAttendanceEntry> { new(4, "A") }),
            "teacher2");

        var status = await service.GetChronicAbsenteeismStatusAsync(4, "2025-2026");

        status.TotalAbsences.Should().Be(10);
        status.Threshold.Should().Be(8);
        status.IsChronicallyAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDailyAttendance_ExcusedAbsence_CountsTowardTotalAbsences_SameAsUnexcused()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 2 starts with no summary/absences. Legacy sp_SaveDailyAttendance recounts
        // TotalAbsences using only `IsAbsent = 1`, with no IsExcused filter, so an excused
        // absence ("E") must count exactly the same as an unexcused one ("A").
        await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, new DateOnly(2025, 10, 20), new List<StudentAttendanceEntry> { new(2, "E", Notes: "Doctor's appointment") }),
            "teacher1");

        var status = await service.GetChronicAbsenteeismStatusAsync(2, "2025-2026");

        status.TotalAbsences.Should().Be(1);

        var history = await service.GetAttendanceHistoryAsync(2, "2025-2026");
        history.Single().Should().Match<AttendanceHistoryEntry>(h => h.IsAbsent && h.IsExcused);
    }

    [Fact]
    public async Task SaveDailyAttendance_WithUnknownAttendanceCode_ThrowsValidationException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, new DateOnly(2025, 10, 1), new List<StudentAttendanceEntry> { new(1, "ZZ") }),
            "teacher1");

        (await act.Should().ThrowAsync<AttendanceValidationException>())
            .Which.Errors.Should().ContainKey("code:1");
    }

    [Fact]
    public async Task SaveDailyAttendance_WithStudentFromAnotherSchool_ThrowsValidationException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        // Student 4 belongs to school 2, not school 1.
        var act = async () => await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, new DateOnly(2025, 10, 1), new List<StudentAttendanceEntry> { new(4, "P") }),
            "teacher1");

        (await act.Should().ThrowAsync<AttendanceValidationException>())
            .Which.Errors.Should().ContainKey("student:4");
    }

    [Fact]
    public async Task SaveDailyAttendance_WithUnknownSchool_ThrowsNotFoundException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(999, new DateOnly(2025, 10, 1), new List<StudentAttendanceEntry> { new(1, "P") }),
            "teacher1");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SaveDailyAttendance_WithNoRecords_ThrowsValidationException()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AttendanceService(db);

        var act = async () => await service.SaveDailyAttendanceAsync(
            new SaveDailyAttendanceRequest(1, new DateOnly(2025, 10, 1), new List<StudentAttendanceEntry>()),
            "teacher1");

        await act.Should().ThrowAsync<AttendanceValidationException>();
    }
}
