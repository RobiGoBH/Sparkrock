using System.Net;
using System.Net.Http.Json;
using AttendanceSystem.API.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AttendanceSystem.Tests;

// End-to-end smoke tests that exercise the real minimal-API pipeline (routing, model
// binding, DI, exception handling) rather than the service directly.
public class AttendanceEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AttendanceEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAttendance_ThenGetHistory_RoundTripsThroughTheApi()
    {
        var attendDate = new DateOnly(2025, 11, 3);
        var request = new SaveDailyAttendanceRequest(1, attendDate, new List<StudentAttendanceEntry>
        {
            new(1, "T", MinutesLate: 5),
        });

        var saveResponse = await _client.PostAsJsonAsync("/api/attendance", request);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await _client.GetAsync("/api/students/1/attendance-history?schoolYear=2025-2026");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<AttendanceHistoryEntry>>();
        history.Should().Contain(h => h.AttendDate == attendDate && h.AttendCode == "T" && h.MinutesLate == 5);
    }

    [Fact]
    public async Task GetChronicAbsenteeism_ForSeededStudent_ReturnsChronicStatus()
    {
        var response = await _client.GetAsync("/api/students/4/chronic-absenteeism?schoolYear=2025-2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<ChronicAbsenteeismStatus>();
        status!.IsChronicallyAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task GetAttendanceHistory_ForUnknownStudent_Returns404()
    {
        var response = await _client.GetAsync("/api/students/999/attendance-history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAttendance_WithUnknownCode_Returns400()
    {
        var request = new SaveDailyAttendanceRequest(1, new DateOnly(2025, 11, 4), new List<StudentAttendanceEntry>
        {
            new(1, "ZZ"),
        });

        var response = await _client.PostAsJsonAsync("/api/attendance", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAttendance_ForUnknownSchool_Returns404()
    {
        var request = new SaveDailyAttendanceRequest(999, new DateOnly(2025, 11, 5), new List<StudentAttendanceEntry>
        {
            new(1, "P"),
        });

        var response = await _client.PostAsJsonAsync("/api/attendance", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChronicAbsenteeism_ForUnknownStudent_Returns404()
    {
        var response = await _client.GetAsync("/api/students/999/chronic-absenteeism");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
