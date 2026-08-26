namespace AttendanceSystem.API.Models;

public class Student
{
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Grade { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
