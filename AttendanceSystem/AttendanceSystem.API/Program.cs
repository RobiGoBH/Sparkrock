using AttendanceSystem.API.Data;
using AttendanceSystem.API.Endpoints;
using AttendanceSystem.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AttendanceDbContext>(options =>
    options.UseInMemoryDatabase("AttendanceSystemDb"));

builder.Services.AddScoped<IAttendanceService, AttendanceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
    SeedData.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        switch (feature?.Error)
        {
            case NotFoundException notFound:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { title = notFound.Message, status = 404 });
                break;
            case AttendanceValidationException validation:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = validation.Message,
                    status = 400,
                    errors = validation.Errors,
                });
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500 });
                break;
        }
    });
});

app.MapAttendanceEndpoints();

app.Run();

public partial class Program
{
}
