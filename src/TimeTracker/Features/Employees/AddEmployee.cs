using TimeTracker.Data;
using TimeTracker.Domain;

namespace TimeTracker.Features.Employees;

public static class AddEmployee
{
    public record Request(string? Name);

    public static void Map(IEndpointRouteBuilder api) => api.MapPost("/employees", async (Request req, AppDb db) =>
    {
        var employee = new Employee(req.Name!);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return Results.Created($"/api/employees/{employee.Id}", EmployeeDto.From(employee));
    });
}
