using AttendanceSystem.API.Contracts;
using AttendanceSystem.API.Services;

namespace AttendanceSystem.API.Endpoints;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithOpenApi();

        group.MapPost("/attendance", async (
            SaveDailyAttendanceRequest request,
            IAttendanceService attendanceService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // No auth model was provided by the legacy artifacts (they used SYSTEM_USER, a SQL
            // login, for audit columns). Falling back to a header lets a caller identify itself
            // for CreatedBy/ModifiedBy until real authentication is added.
            var submittedBy = httpContext.Request.Headers["X-User"].FirstOrDefault() ?? "api";
            var response = await attendanceService.SaveDailyAttendanceAsync(request, submittedBy, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("SaveDailyAttendance")
        .WithSummary("Save (upsert) a batch of daily attendance records for a school and date.")
        .Produces<SaveDailyAttendanceResponse>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/students/{studentId:int}/attendance-history", async (
            int studentId,
            string? schoolYear,
            IAttendanceService attendanceService,
            CancellationToken cancellationToken) =>
        {
            var history = await attendanceService.GetAttendanceHistoryAsync(studentId, schoolYear, cancellationToken);
            return Results.Ok(history);
        })
        .WithName("GetAttendanceHistory")
        .WithSummary("Retrieve a student's attendance history for a school year (defaults to the current one).")
        .Produces<List<AttendanceHistoryEntry>>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/students/{studentId:int}/chronic-absenteeism", async (
            int studentId,
            string? schoolYear,
            IAttendanceService attendanceService,
            CancellationToken cancellationToken) =>
        {
            var status = await attendanceService.GetChronicAbsenteeismStatusAsync(studentId, schoolYear, cancellationToken);
            return Results.Ok(status);
        })
        .WithName("GetChronicAbsenteeismStatus")
        .WithSummary("Return whether a student is chronically absent for a school year (defaults to the current one).")
        .Produces<ChronicAbsenteeismStatus>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
