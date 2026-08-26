namespace AttendanceSystem.API.Services;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class AttendanceValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AttendanceValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more attendance records failed validation.")
    {
        Errors = errors;
    }
}
