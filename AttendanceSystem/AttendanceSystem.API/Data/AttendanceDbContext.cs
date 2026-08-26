using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Data;

public class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<AttendanceCode> AttendanceCodes => Set<AttendanceCode>();
    public DbSet<StudentAttendance> StudentAttendances => Set<StudentAttendance>();
    public DbSet<StudentAttendanceSummary> StudentAttendanceSummaries => Set<StudentAttendanceSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Key property names mirror the legacy schema.sql column names (CodeID, AttendanceID,
        // SummaryID) rather than EF's "<TypeName>Id"/"Id" convention, so they need to be
        // configured explicitly.
        modelBuilder.Entity<AttendanceCode>().HasKey(c => c.CodeId);
        modelBuilder.Entity<StudentAttendance>().HasKey(a => a.AttendanceId);
        modelBuilder.Entity<StudentAttendanceSummary>().HasKey(s => s.SummaryId);

        // The legacy schema never enforced this, relying purely on application logic in
        // sp_SaveDailyAttendance's lookup-then-insert-or-update. Enforcing it here closes
        // a duplicate-row race condition that existed in the original design.
        modelBuilder.Entity<StudentAttendance>()
            .HasIndex(a => new { a.StudentId, a.AttendDate })
            .IsUnique();

        modelBuilder.Entity<StudentAttendanceSummary>()
            .HasIndex(s => new { s.StudentId, s.SchoolYear })
            .IsUnique();

        modelBuilder.Entity<AttendanceCode>()
            .HasIndex(c => c.CodeValue)
            .IsUnique();
    }
}
