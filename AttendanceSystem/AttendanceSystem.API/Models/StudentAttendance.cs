namespace AttendanceSystem.API.Models;

public class StudentAttendance
{
    public int AttendanceId { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public DateOnly AttendDate { get; set; }
    public string AttendCode { get; set; } = default!;
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public int MinutesLate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
