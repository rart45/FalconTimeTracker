using Microsoft.EntityFrameworkCore;
using FalconTimeTracker.Data;
using FalconTimeTracker.Domain;

namespace FalconTimeTracker.Features.Reports;

public static class GetReport
{
    public record Entry(int Id, string Task, DateTime StartedAt, DateTime? EndedAt, long Seconds);
    public record Row(int EmployeeId, string Name, long TotalSeconds, List<Entry> Entries);
    public record Response(DateTime From, DateTime To, long TotalSeconds, List<Row> Employees);

    // Отчёт по всем сотрудникам, включая тех, у кого за период нет записей.
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet("/report", async (DateTime from, DateTime to, AppDb db, TimeProvider clock) =>
        {
            var period = new TimePeriod(from, to);
            var now = clock.GetUtcNow().UtcDateTime;

            var employees = await db.Employees
                .Include(e => e.Entries
                    .Where(x => x.StartedAt < period.To && (x.EndedAt == null || x.EndedAt > period.From))
                    .OrderBy(x => x.StartedAt))
                .OrderBy(e => e.Name)
                .ToListAsync();

            var rows = employees.Select(emp =>
            {
                var entries = emp.Entries
                    .Select(e => new Entry(e.Id, e.Task, e.StartedAt, e.EndedAt, (long)period.Overlap(e, now).TotalSeconds))
                    .ToList();
                return new Row(emp.Id, emp.Name, entries.Sum(e => e.Seconds), entries);
            }).ToList();

            return new Response(period.From, period.To, rows.Sum(r => r.TotalSeconds), rows);
        });
}
