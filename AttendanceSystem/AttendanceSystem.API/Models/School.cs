namespace AttendanceSystem.API.Models;

// Not present in the legacy schema.sql, but both stored procedures reference Schools
// (SchoolName, AbsenceAlertThreshold). Reconstructed here with the minimal columns
// the legacy behavior implies.
public class School
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = default!;
    public bool Active { get; set; } = true;
    public int AbsenceAlertThreshold { get; set; } = 10;
}
