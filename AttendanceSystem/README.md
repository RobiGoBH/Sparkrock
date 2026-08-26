# Attendance System — .NET 8 Rewrite

A from-scratch replacement for the legacy VB6/T-SQL daily attendance module (`frmDailyAttendance.frm`, `sp_SaveDailyAttendance`, `sp_GetStudentAttendance`). Implements the three required scenarios: save daily attendance, retrieve attendance history, and return chronic-absenteeism status.

## Architecture

- **.NET 8 Minimal APIs** (`AttendanceSystem.API`) — three endpoints only, so a controller-per-resource MVC setup would have been pure ceremony. Routing lives in `Endpoints/AttendanceEndpoints.cs`; request/response shapes are immutable `record`s in `Contracts/`.
- **EF Core InMemory** (`Data/AttendanceDbContext.cs`) as required, seeded on startup (`Data/SeedData.cs`) with sample students, schools, attendance codes, and pre-existing attendance history.
- **Service layer** (`Services/AttendanceService.cs`, behind `IAttendanceService`) holds all business logic, independent of HTTP — endpoints are thin adapters. This is what `AttendanceSystem.Tests` exercises directly (36 unit tests) plus a handful of `WebApplicationFactory` tests that go through real routing/DI/exception handling.
- Domain errors (`NotFoundException`, `AttendanceValidationException`) are mapped to 404/400 by exception-handling middleware in `Program.cs`, rather than being encoded as HTTP concerns inside the service.

## Business logic ported from the legacy SQL

- **School year = September 1 cutoff.** `SchoolYearCalculator.GetSchoolYear` reproduces `MONTH(@date) >= 9` from both stored procedures exactly, but centralizes it in one place instead of duplicating the calculation in every proc.
- **Chronic absenteeism = absolute count, not a percentage.** `School.AbsenceAlertThreshold` defaults to `10`, mirroring `ISNULL(sc.AbsenceAlertThreshold, 10)`, and the check is a plain `TotalAbsences >= threshold` — no percent-of-enrolled-days logic exists anywhere, matching the legacy behavior even though that's a coarser definition than how "chronic absenteeism" is usually defined in K-12 policy.
- **`IsAbsent` counts regardless of `IsExcused`.** The recount query filters only on `IsAbsent`, with no `IsExcused` condition, exactly like `sp_SaveDailyAttendance`'s `COUNT(*) ... WHERE IsAbsent = 1`. Excused and unexcused absences both count toward the threshold.
- **Upsert by (StudentId, AttendDate)**, same natural key the legacy proc used — but now backed by a unique index (`AttendanceDbContext`), closing a real gap: the legacy schema never enforced this, so concurrent saves could have raced into duplicate rows.

## Eliminating the legacy N+1 cursor

`sp_SaveDailyAttendance` processed rows with a `DECLARE cur CURSOR ... FETCH NEXT` loop — one lookup, one insert/update, one recount, and one alert check per student, per save. A naive EF Core port reproduces the same shape in C# (a `FirstOrDefaultAsync`/`CountAsync` per loop iteration), so the save endpoint was refactored to batch instead:

1. One `Where(... studentIds.Contains(...))` query loads all existing attendance rows for the batch into a dictionary; the per-record loop only touches that dictionary and the change tracker (no DB calls in the loop).
2. One `SaveChangesAsync` persists the upserts.
3. One `GroupBy(a => a.StudentId).Select(...Count())` aggregate query recomputes every affected student's absence total in a single round trip, replacing a `COUNT(*)` per student.
4. One batch fetch of existing summaries, updated in-memory, then one final `SaveChangesAsync`.

Total DB round trips per save is now fixed (8), independent of batch size — verified by `SaveDailyAttendance_WithLargeBatchOfStudents_UpsertsAllRecordsInOneBatch`, a 50-student batch saved twice to also confirm idempotent upserts.

## What couldn't be determined from the legacy code, and how it was handled

- `Schools`, `SchoolTerms`, and `AttendanceSubmissionLog` are referenced by the stored procedures but never defined in `schema.sql`. Reconstructed the minimal `School` shape the procs imply (`SchoolName`, `AbsenceAlertThreshold`); dropped `SchoolTerms`/submission-log/`StudentAlerts` since they're not part of the three required scenarios.
- Valid `AttendanceCodes` values were never enumerated — seeded a plausible set (P/A/E/T/H). An unrecognized code is rejected with `400` instead of the legacy's silent fallback to "not absent," since masking bad data entry seemed worse than surfacing it.
- No auth model was provided (legacy relied on `SYSTEM_USER`, a SQL login). The save endpoint accepts an optional `X-User` header for the audit `CreatedBy`/`ModifiedBy` fields as a placeholder until real authentication is added.
