using AttendanceSystem.API.Contracts;
using AttendanceSystem.API.Data;
using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Services;

public class AttendanceService : IAttendanceService
{
    private readonly AttendanceDbContext _db;

    public AttendanceService(AttendanceDbContext db)
    {
        _db = db;
    }

    public async Task<SaveDailyAttendanceResponse> SaveDailyAttendanceAsync(
        SaveDailyAttendanceRequest request,
        string? submittedBy,
        CancellationToken cancellationToken = default)
    {
        var school = await _db.Schools.FindAsync(new object?[] { request.SchoolId }, cancellationToken)
            ?? throw new NotFoundException($"School {request.SchoolId} was not found.");

        if (request.Records.Count == 0)
        {
            throw new AttendanceValidationException(new Dictionary<string, string[]>
            {
                ["Records"] = new[] { "At least one attendance record is required." },
            });
        }

        var studentIds = request.Records.Select(r => r.StudentId).Distinct().ToList();
        var students = await _db.Students
            .Where(s => studentIds.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId, cancellationToken);

        var codeValues = request.Records.Select(r => r.AttendCode).Distinct().ToList();
        var codes = await _db.AttendanceCodes
            .Where(c => codeValues.Contains(c.CodeValue) && c.IsActive)
            .ToDictionaryAsync(c => c.CodeValue, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        foreach (var record in request.Records)
        {
            if (!students.TryGetValue(record.StudentId, out var student))
            {
                errors[$"student:{record.StudentId}"] = new[] { $"Student {record.StudentId} was not found." };
            }
            else if (student.SchoolId != request.SchoolId)
            {
                errors[$"student:{record.StudentId}"] = new[] { $"Student {record.StudentId} does not belong to school {request.SchoolId}." };
            }

            if (!codes.ContainsKey(record.AttendCode))
            {
                errors[$"code:{record.StudentId}"] = new[] { $"'{record.AttendCode}' is not a recognized, active attendance code." };
            }
        }

        if (errors.Count > 0)
        {
            throw new AttendanceValidationException(errors);
        }

        // Batch-fetch every attendance row that could collide with this save (one query for
        // the whole request), instead of the legacy cursor's row-by-row
        // "does a record already exist for this student/date" lookup.
        var existingAttendanceByStudent = await _db.StudentAttendances
            .Where(a => a.AttendDate == request.AttendDate && studentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId, cancellationToken);

        foreach (var record in request.Records)
        {
            var code = codes[record.AttendCode];

            if (existingAttendanceByStudent.TryGetValue(record.StudentId, out var existing))
            {
                existing.AttendCode = code.CodeValue;
                existing.IsAbsent = code.IsAbsent;
                existing.IsExcused = code.IsExcused;
                existing.MinutesLate = record.MinutesLate;
                existing.Notes = record.Notes;
                existing.ModifiedDate = DateTime.UtcNow;
                existing.ModifiedBy = submittedBy;
            }
            else
            {
                var newAttendance = new StudentAttendance
                {
                    StudentId = record.StudentId,
                    SchoolId = request.SchoolId,
                    AttendDate = request.AttendDate,
                    AttendCode = code.CodeValue,
                    IsAbsent = code.IsAbsent,
                    IsExcused = code.IsExcused,
                    MinutesLate = record.MinutesLate,
                    Notes = record.Notes,
                    CreatedBy = submittedBy,
                };
                _db.StudentAttendances.Add(newAttendance);

                // Keep the dictionary in sync so a duplicate StudentId later in the same
                // request updates this row instead of inserting a second one.
                existingAttendanceByStudent[record.StudentId] = newAttendance;
            }
        }

        // Persist the attendance upserts first: the summary recount below queries the store
        // directly, so it wouldn't see rows that are only pending in the change tracker.
        await _db.SaveChangesAsync(cancellationToken);

        var schoolYear = SchoolYearCalculator.GetSchoolYear(request.AttendDate);
        var (yearStart, yearEnd) = SchoolYearCalculator.GetDateRange(schoolYear);

        // Recount (not increment) absences for every affected student, mirroring the legacy
        // recompute-on-save behavior — as one grouped aggregate query and one batch summary
        // fetch, instead of a COUNT(*) + summary lookup per student.
        var absenceCountsByStudent = await _db.StudentAttendances
            .Where(a => a.SchoolId == request.SchoolId
                        && a.IsAbsent
                        && a.AttendDate >= yearStart
                        && a.AttendDate <= yearEnd
                        && studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count, cancellationToken);

        var existingSummariesByStudent = await _db.StudentAttendanceSummaries
            .Where(s => s.SchoolYear == schoolYear && studentIds.Contains(s.StudentId))
            .ToDictionaryAsync(s => s.StudentId, cancellationToken);

        foreach (var studentId in studentIds)
        {
            var totalAbsences = absenceCountsByStudent.GetValueOrDefault(studentId);

            if (existingSummariesByStudent.TryGetValue(studentId, out var summary))
            {
                summary.TotalAbsences = totalAbsences;
                summary.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                _db.StudentAttendanceSummaries.Add(new StudentAttendanceSummary
                {
                    StudentId = studentId,
                    SchoolId = request.SchoolId,
                    SchoolYear = schoolYear,
                    TotalAbsences = totalAbsences,
                    LastUpdated = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new SaveDailyAttendanceResponse(request.SchoolId, request.AttendDate, request.Records.Count);
    }

    public async Task<List<AttendanceHistoryEntry>> GetAttendanceHistoryAsync(
        int studentId,
        string? schoolYear,
        CancellationToken cancellationToken = default)
    {
        await GetStudentOrThrowAsync(studentId, cancellationToken);

        var year = ResolveSchoolYear(schoolYear);
        var (yearStart, yearEnd) = SchoolYearCalculator.GetDateRange(year);

        var history = await (
            from a in _db.StudentAttendances
            join c in _db.AttendanceCodes on a.AttendCode equals c.CodeValue
            where a.StudentId == studentId && a.AttendDate >= yearStart && a.AttendDate <= yearEnd
            orderby a.AttendDate descending
            select new AttendanceHistoryEntry(a.AttendDate, a.AttendCode, c.Description, a.IsAbsent, a.IsExcused, a.MinutesLate, a.Notes)
        ).ToListAsync(cancellationToken);

        return history;
    }

    public async Task<ChronicAbsenteeismStatus> GetChronicAbsenteeismStatusAsync(
        int studentId,
        string? schoolYear,
        CancellationToken cancellationToken = default)
    {
        var student = await GetStudentOrThrowAsync(studentId, cancellationToken);
        var school = await _db.Schools.FindAsync(new object?[] { student.SchoolId }, cancellationToken)
            ?? throw new NotFoundException($"School {student.SchoolId} was not found.");

        var year = ResolveSchoolYear(schoolYear);

        var summary = await _db.StudentAttendanceSummaries.FirstOrDefaultAsync(
            s => s.StudentId == studentId && s.SchoolYear == year,
            cancellationToken);

        var totalAbsences = summary?.TotalAbsences ?? 0;
        var threshold = school.AbsenceAlertThreshold;

        return new ChronicAbsenteeismStatus(
            studentId,
            year,
            totalAbsences,
            threshold,
            totalAbsences >= threshold,
            summary?.LastUpdated);
    }

    private async Task<Student> GetStudentOrThrowAsync(int studentId, CancellationToken cancellationToken) =>
        await _db.Students.FindAsync(new object?[] { studentId }, cancellationToken)
            ?? throw new NotFoundException($"Student {studentId} was not found.");

    private static string ResolveSchoolYear(string? schoolYear)
    {
        if (string.IsNullOrWhiteSpace(schoolYear))
        {
            return SchoolYearCalculator.GetCurrentSchoolYear();
        }

        if (!SchoolYearCalculator.IsValid(schoolYear))
        {
            throw new AttendanceValidationException(new Dictionary<string, string[]>
            {
                ["schoolYear"] = new[] { $"'{schoolYear}' is not a valid school year (expected format 'YYYY-YYYY+1')." },
            });
        }

        return schoolYear;
    }
}
