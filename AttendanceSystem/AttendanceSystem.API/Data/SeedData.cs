using AttendanceSystem.API.Models;
using AttendanceSystem.API.Services;

namespace AttendanceSystem.API.Data;

public static class SeedData
{
    public static void Seed(AttendanceDbContext context)
    {
        if (context.Schools.Any())
        {
            return;
        }

        var schools = new[]
        {
            new School { SchoolId = 1, SchoolName = "Lincoln Elementary", Active = true, AbsenceAlertThreshold = 10 },
            new School { SchoolId = 2, SchoolName = "Roosevelt Middle School", Active = true, AbsenceAlertThreshold = 8 },
        };
        context.Schools.AddRange(schools);

        var codes = new[]
        {
            new AttendanceCode { CodeId = 1, CodeValue = "P", Description = "Present", IsAbsent = false, IsExcused = false },
            new AttendanceCode { CodeId = 2, CodeValue = "A", Description = "Absent - Unexcused", IsAbsent = true, IsExcused = false },
            new AttendanceCode { CodeId = 3, CodeValue = "E", Description = "Absent - Excused", IsAbsent = true, IsExcused = true },
            new AttendanceCode { CodeId = 4, CodeValue = "T", Description = "Tardy", IsAbsent = false, IsExcused = false },
            new AttendanceCode { CodeId = 5, CodeValue = "H", Description = "Half Day", IsAbsent = false, IsExcused = false },
        };
        context.AttendanceCodes.AddRange(codes);

        var students = new[]
        {
            new Student { StudentId = 1, SchoolId = 1, FirstName = "Ava", LastName = "Thompson", Grade = "3", DateOfBirth = new DateOnly(2017, 4, 12), Active = true },
            new Student { StudentId = 2, SchoolId = 1, FirstName = "Noah", LastName = "Martinez", Grade = "3", DateOfBirth = new DateOnly(2017, 8, 2), Active = true },
            new Student { StudentId = 3, SchoolId = 1, FirstName = "Mia", LastName = "Chen", Grade = "4", DateOfBirth = new DateOnly(2016, 1, 20), Active = true },
            new Student { StudentId = 4, SchoolId = 2, FirstName = "Liam", LastName = "Johnson", Grade = "7", DateOfBirth = new DateOnly(2013, 11, 5), Active = true },
            new Student { StudentId = 5, SchoolId = 2, FirstName = "Sophia", LastName = "Davis", Grade = "7", DateOfBirth = new DateOnly(2013, 6, 30), Active = true },
            new Student { StudentId = 6, SchoolId = 2, FirstName = "Ethan", LastName = "Wilson", Grade = "8", DateOfBirth = new DateOnly(2012, 3, 17), Active = false },
        };
        context.Students.AddRange(students);

        // Fixed (not wall-clock-derived) so the seed stays internally consistent no matter
        // when the app happens to run: the summary rows below are a straight recount of the
        // attendance rows also seeded here, all dated within this same school year.
        var seedSchoolYear = SchoolYearCalculator.GetSchoolYear(new DateOnly(2025, 9, 3));

        // A few historical attendance rows so GET history has data to return out of the box.
        var attendanceHistory = new List<StudentAttendance>
        {
            new() { AttendanceId = 1, StudentId = 1, SchoolId = 1, AttendDate = new DateOnly(2025, 9, 3), AttendCode = "P", IsAbsent = false, IsExcused = false, MinutesLate = 0, CreatedBy = "seed" },
            new() { AttendanceId = 2, StudentId = 1, SchoolId = 1, AttendDate = new DateOnly(2025, 9, 4), AttendCode = "T", IsAbsent = false, IsExcused = false, MinutesLate = 10, CreatedBy = "seed" },
            new() { AttendanceId = 3, StudentId = 1, SchoolId = 1, AttendDate = new DateOnly(2025, 9, 5), AttendCode = "A", IsAbsent = true, IsExcused = false, MinutesLate = 0, CreatedBy = "seed" },
        };

        // Student 4 is seeded already over their school's chronic-absenteeism threshold (8),
        // backed by real absence rows (not just a summary number), so GET chronic-absenteeism
        // status has an interesting result out of the box and stays correct after future saves
        // recompute the summary from these rows.
        var studentFourAbsenceDates = new[]
        {
            new DateOnly(2025, 9, 8), new DateOnly(2025, 9, 9), new DateOnly(2025, 9, 10),
            new DateOnly(2025, 9, 11), new DateOnly(2025, 9, 12), new DateOnly(2025, 9, 15),
            new DateOnly(2025, 9, 16), new DateOnly(2025, 9, 17), new DateOnly(2025, 9, 18),
        };
        var nextAttendanceId = 4;
        foreach (var date in studentFourAbsenceDates)
        {
            attendanceHistory.Add(new StudentAttendance
            {
                AttendanceId = nextAttendanceId++,
                StudentId = 4,
                SchoolId = 2,
                AttendDate = date,
                AttendCode = "A",
                IsAbsent = true,
                IsExcused = false,
                MinutesLate = 0,
                CreatedBy = "seed",
            });
        }

        context.StudentAttendances.AddRange(attendanceHistory);

        var summaries = new[]
        {
            new StudentAttendanceSummary { SummaryId = 1, StudentId = 1, SchoolId = 1, SchoolYear = seedSchoolYear, TotalAbsences = 1 },
            new StudentAttendanceSummary { SummaryId = 2, StudentId = 4, SchoolId = 2, SchoolYear = seedSchoolYear, TotalAbsences = 9 },
        };
        context.StudentAttendanceSummaries.AddRange(summaries);

        context.SaveChanges();
    }
}
