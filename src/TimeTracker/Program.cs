using Microsoft.EntityFrameworkCore;
using TimeTracker;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>((sp, o) =>
    o.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Db")));
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/employees", async (AppDb db) =>
{
    var employees = await db.Employees.OrderBy(e => e.Name).ToListAsync();
    var running = await db.Entries.Where(e => e.EndedAt == null).ToDictionaryAsync(e => e.EmployeeId);
    return employees.Select(e => new EmployeeDto(e.Id, e.Name, running.GetValueOrDefault(e.Id)));
});

api.MapPost("/employees", async (NewEmployee req, AppDb db) =>
{
    var name = req.Name?.Trim();
    if (string.IsNullOrEmpty(name) || name.Length > 100)
        return Results.BadRequest(new { error = "Имя обязательно, не длиннее 100 символов" });

    var employee = new Employee { Name = name };
    db.Employees.Add(employee);
    await db.SaveChangesAsync();
    return Results.Created($"/api/employees/{employee.Id}", new EmployeeDto(employee.Id, employee.Name, null));
});

api.MapPost("/employees/{id:int}/timer/start", async (int id, StartTimer req, AppDb db, TimeProvider clock) =>
{
    var task = req.Task?.Trim();
    if (string.IsNullOrEmpty(task) || task.Length > 500)
        return Results.BadRequest(new { error = "Описание задачи обязательно, не длиннее 500 символов" });
    if (!await db.Employees.AnyAsync(e => e.Id == id))
        return Results.NotFound(new { error = "Сотрудник не найден" });
    if (await db.Entries.AnyAsync(e => e.EmployeeId == id && e.EndedAt == null))
        return Results.Conflict(new { error = "Таймер уже запущен" });

    var entry = new TimeEntry { EmployeeId = id, Task = task, StartedAt = clock.GetUtcNow().UtcDateTime };
    db.Entries.Add(entry);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException) // второй запрос успел между проверкой и вставкой, сработал уникальный индекс
    {
        return Results.Conflict(new { error = "Таймер уже запущен" });
    }
    return Results.Created($"/api/entries/{entry.Id}", entry);
});

api.MapPost("/employees/{id:int}/timer/stop", async (int id, AppDb db, TimeProvider clock) =>
{
    var entry = await db.Entries.SingleOrDefaultAsync(e => e.EmployeeId == id && e.EndedAt == null);
    if (entry is null)
        return Results.Conflict(new { error = "Нет запущенного таймера" });

    entry.EndedAt = clock.GetUtcNow().UtcDateTime;
    await db.SaveChangesAsync();
    return Results.Ok(entry);
});

// Отчёт за [from, to). Записи на границе периода обрезаются по ней, запущенный таймер считается до текущего момента.
api.MapGet("/report", async (DateTime from, DateTime to, AppDb db, TimeProvider clock) =>
{
    from = from.ToUniversalTime();
    to = to.ToUniversalTime();
    if (from >= to)
        return Results.BadRequest(new { error = "Начало периода должно быть раньше конца" });

    var now = clock.GetUtcNow().UtcDateTime;
    var employees = await db.Employees.OrderBy(e => e.Name).ToListAsync();
    var entries = await db.Entries
        .Where(e => e.StartedAt < to && (e.EndedAt == null || e.EndedAt > from))
        .OrderBy(e => e.StartedAt)
        .ToListAsync();

    var rows = employees.Select(emp =>
    {
        var items = entries.Where(e => e.EmployeeId == emp.Id).Select(e =>
        {
            var start = e.StartedAt < from ? from : e.StartedAt;
            var end = Min(e.EndedAt ?? now, to);
            return new ReportEntry(e.Id, e.Task, e.StartedAt, e.EndedAt, (long)Math.Max(0, (end - start).TotalSeconds));
        }).ToList();
        return new ReportRow(emp.Id, emp.Name, items.Sum(i => i.Seconds), items);
    }).ToList();

    return Results.Ok(new Report(from, to, rows.Sum(r => r.TotalSeconds), rows));

    static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
});

app.Run();

record NewEmployee(string? Name);
record StartTimer(string? Task);
record EmployeeDto(int Id, string Name, TimeEntry? Running);
record ReportEntry(int Id, string Task, DateTime StartedAt, DateTime? EndedAt, long Seconds);
record ReportRow(int EmployeeId, string Name, long TotalSeconds, List<ReportEntry> Entries);
record Report(DateTime From, DateTime To, long TotalSeconds, List<ReportRow> Employees);
