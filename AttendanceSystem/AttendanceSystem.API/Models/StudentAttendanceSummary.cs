namespace AttendanceSystem.API.Models;

public class StudentAttendanceSummary
{
    public int SummaryId { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public string SchoolYear { get; set; } = default!;
    public int TotalAbsences { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
