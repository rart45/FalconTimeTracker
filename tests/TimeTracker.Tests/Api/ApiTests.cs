using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TimeTracker.Tests;

public class ApiTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"timetracker-test-{Guid.NewGuid()}.db");
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Db", $"Data Source={_dbPath};Pooling=False");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock)));
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Full_cycle_counts_time_in_report()
    {
        var id = await AddEmployee("Иван Тестов");

        (await Start(id, "Вёрстка формы")).EnsureSuccessStatusCode();
        _clock.Advance(TimeSpan.FromMinutes(90));
        (await Stop(id)).EnsureSuccessStatusCode();

        var report = await Report(Day(30), Day(31));
        var row = Assert.Single(report.Employees);
        Assert.Equal(90 * 60, row.TotalSeconds);
        Assert.Equal("Вёрстка формы", Assert.Single(row.Entries).Task);
    }

    [Fact]
    public async Task Starting_timer_twice_returns_conflict_and_keeps_one_entry()
    {
        var id = await AddEmployee("Иван Тестов");

        Assert.Equal(HttpStatusCode.Created, (await Start(id, "Первая задача")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(id, "Вторая задача")).StatusCode);

        _clock.Advance(TimeSpan.FromMinutes(10));
        var entry = Assert.Single((await Report(Day(30), Day(31))).Employees[0].Entries);
        Assert.Equal("Первая задача", entry.Task);
    }

    [Fact]
    public async Task Concurrent_starts_create_only_one_entry()
    {
        var id = await AddEmployee("Иван Тестов");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Start(id, $"Задача {i}")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Timer_can_be_restarted_after_stop()
    {
        var id = await AddEmployee("Иван Тестов");

        await Start(id, "Утро");
        _clock.Advance(TimeSpan.FromMinutes(30));
        await Stop(id);
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.Created, (await Start(id, "После обеда")).StatusCode);
        _clock.Advance(TimeSpan.FromMinutes(15));
        await Stop(id);

        var row = (await Report(Day(30), Day(31))).Employees[0];
        Assert.Equal(2, row.Entries.Count);
        Assert.Equal(45 * 60, row.TotalSeconds);
    }

    [Fact]
    public async Task Stop_without_running_timer_returns_conflict()
    {
        var id = await AddEmployee("Иван Тестов");
        Assert.Equal(HttpStatusCode.Conflict, (await Stop(id)).StatusCode);
    }

    [Fact]
    public async Task Empty_report_without_employees()
    {
        var report = await Report(Day(30), Day(31));
        Assert.Empty(report.Employees);
        Assert.Equal(0, report.TotalSeconds);
    }

    [Fact]
    public async Task Empty_report_for_period_without_entries()
    {
        var id = await AddEmployee("Иван Тестов");
        await Start(id, "Задача");
        _clock.Advance(TimeSpan.FromHours(1));
        await Stop(id);

        var report = await Report(Day(1), Day(2));

        var row = Assert.Single(report.Employees);
        Assert.Empty(row.Entries);
        Assert.Equal(0, row.TotalSeconds);
    }

    [Fact]
    public async Task Running_timer_is_counted_until_now()
    {
        var id = await AddEmployee("Иван Тестов");
        await Start(id, "Задача");
        _clock.Advance(TimeSpan.FromMinutes(20));

        var entry = Assert.Single((await Report(Day(30), Day(31))).Employees[0].Entries);
        Assert.Null(entry.EndedAt);
        Assert.Equal(20 * 60, entry.Seconds);
    }

    [Fact]
    public async Task Entry_crossing_period_border_is_clipped()
    {
        var id = await AddEmployee("Иван Тестов");
        _clock.Set(new DateTime(2026, 10, 30, 23, 0, 0, DateTimeKind.Utc));
        await Start(id, "Ночной релиз");
        _clock.Advance(TimeSpan.FromHours(2));
        await Stop(id);

        Assert.Equal(3600, (await Report(Day(30), Day(31))).TotalSeconds);
        Assert.Equal(3600, (await Report(Day(31), Day(32))).TotalSeconds);
    }

    [Fact]
    public async Task Data_survives_restart()
    {
        var id = await AddEmployee("Иван Тестов");
        await Start(id, "Задача");
        _clock.Advance(TimeSpan.FromMinutes(10));
        await Stop(id);

        // Новая фабрика = новый процесс приложения на том же файле БД.
        using var restarted = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Db", $"Data Source={_dbPath};Pooling=False");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock)));
        });
        var report = await restarted.CreateClient().GetFromJsonAsync<ReportDto>($"/api/report?from={Iso(Day(30))}&to={Iso(Day(31))}");

        Assert.Equal(600, report!.TotalSeconds);
    }

    [Fact]
    public async Task Times_are_returned_as_utc()
    {
        var id = await AddEmployee("Иван Тестов");
        await Start(id, "Задача");

        var json = await _client.GetStringAsync("/api/employees");

        Assert.Contains("\"startedAt\":\"2026-10-30T09:00:00Z\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validation_rejects_empty_values(string value)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/employees", new { name = value })).StatusCode);

        var id = await AddEmployee("Иван Тестов");
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(id, value)).StatusCode);
    }

    [Fact]
    public async Task Start_for_unknown_employee_returns_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Start(999, "Задача")).StatusCode);
    }

    [Fact]
    public async Task Report_with_inverted_period_returns_bad_request()
    {
        var res = await _client.GetAsync($"/api/report?from={Iso(Day(31))}&to={Iso(Day(30))}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private static DateTime Day(int day) => new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1);
    private static string Iso(DateTime d) => Uri.EscapeDataString(d.ToString("O"));

    private async Task<int> AddEmployee(string name)
    {
        var res = await _client.PostAsJsonAsync("/api/employees", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<EmployeeResponse>())!.Id;
    }

    private Task<HttpResponseMessage> Start(int id, string task) =>
        _client.PostAsJsonAsync($"/api/employees/{id}/timer/start", new { task });

    private Task<HttpResponseMessage> Stop(int id) =>
        _client.PostAsync($"/api/employees/{id}/timer/stop", null);

    private async Task<ReportDto> Report(DateTime from, DateTime to) =>
        (await _client.GetFromJsonAsync<ReportDto>($"/api/report?from={Iso(from)}&to={Iso(to)}"))!;

    private record EmployeeResponse(int Id, string Name);
    private record EntryDto(int Id, string Task, DateTime StartedAt, DateTime? EndedAt, long Seconds);
    private record RowDto(int EmployeeId, string Name, long TotalSeconds, List<EntryDto> Entries);
    private record ReportDto(long TotalSeconds, List<RowDto> Employees);

    private class ManualClock(DateTime start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
        public void Set(DateTime at) => _now = at;
    }
}
