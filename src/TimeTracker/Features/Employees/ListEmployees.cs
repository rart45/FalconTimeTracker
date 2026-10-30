using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Domain;

namespace TimeTracker.Features.Employees;

public record EntryDto(int Id, string Task, DateTime StartedAt, DateTime? EndedAt)
{
    public static EntryDto From(TimeEntry e) => new(e.Id, e.Task, e.StartedAt, e.EndedAt);
}

public record EmployeeDto(int Id, string Name, EntryDto? Running)
{
    public static EmployeeDto From(Employee e) =>
        new(e.Id, e.Name, e.RunningEntry is { } running ? EntryDto.From(running) : null);
}

public static class ListEmployees
{
    public static void Map(IEndpointRouteBuilder api) => api.MapGet("/employees", async (AppDb db) =>
    {
        var employees = await db.Employees
            .Include(e => e.Entries.Where(x => x.EndedAt == null))
            .OrderBy(e => e.Name)
            .ToListAsync();
        return employees.Select(EmployeeDto.From);
    });
}
