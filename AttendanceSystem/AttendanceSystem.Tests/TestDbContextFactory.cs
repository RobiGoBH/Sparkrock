using AttendanceSystem.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Tests;

internal static class TestDbContextFactory
{
    // Each test gets its own isolated in-memory database so tests can run in parallel
    // without stepping on each other's data.
    public static AttendanceDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AttendanceDbContext(options);
        SeedData.Seed(context);
        return context;
    }
}
