using AttendanceSystem.API.Contracts;

namespace AttendanceSystem.API.Services;

public interface IAttendanceService
{
    Task<SaveDailyAttendanceResponse> SaveDailyAttendanceAsync(
        SaveDailyAttendanceRequest request,
        string? submittedBy,
        CancellationToken cancellationToken = default);

    Task<List<AttendanceHistoryEntry>> GetAttendanceHistoryAsync(
        int studentId,
        string? schoolYear,
        CancellationToken cancellationToken = default);

    Task<ChronicAbsenteeismStatus> GetChronicAbsenteeismStatusAsync(
        int studentId,
        string? schoolYear,
        CancellationToken cancellationToken = default);
}
