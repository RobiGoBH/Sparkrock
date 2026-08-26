namespace AttendanceSystem.API.Contracts;

public record SaveDailyAttendanceRequest(
    int SchoolId,
    DateOnly AttendDate,
    List<StudentAttendanceEntry> Records);

public record StudentAttendanceEntry(
    int StudentId,
    string AttendCode,
    int MinutesLate = 0,
    string? Notes = null);

public record SaveDailyAttendanceResponse(
    int SchoolId,
    DateOnly AttendDate,
    int RecordsSaved);

public record AttendanceHistoryEntry(
    DateOnly AttendDate,
    string AttendCode,
    string AttendCodeDescription,
    bool IsAbsent,
    bool IsExcused,
    int MinutesLate,
    string? Notes);

public record ChronicAbsenteeismStatus(
    int StudentId,
    string SchoolYear,
    int TotalAbsences,
    int Threshold,
    bool IsChronicallyAbsent,
    DateTime? LastUpdated);
