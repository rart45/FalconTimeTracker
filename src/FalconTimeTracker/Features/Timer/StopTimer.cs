using FalconTimeTracker.Data;
using FalconTimeTracker.Features.Employees;

namespace FalconTimeTracker.Features.Timer;

public static class StopTimer
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/employees/{id:int}/timer/stop", async (int id, AppDb db, TimeProvider clock) =>
        {
            var employee = await db.FindWithRunningTimerAsync(id);
            if (employee is null)
                return Results.NotFound(new { error = "Сотрудник не найден" });

            var entry = employee.StopTimer(clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            return Results.Ok(EntryDto.From(entry));
        });
}
