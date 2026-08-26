namespace AttendanceSystem.API.Models;

public class AttendanceCode
{
    public int CodeId { get; set; }
    public string CodeValue { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsAbsent { get; set; }
    public bool IsExcused { get; set; }
    public bool IsActive { get; set; } = true;
}
