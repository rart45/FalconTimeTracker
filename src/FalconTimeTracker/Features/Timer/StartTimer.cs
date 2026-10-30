using Microsoft.EntityFrameworkCore;
using FalconTimeTracker.Data;
using FalconTimeTracker.Domain;
using FalconTimeTracker.Features.Employees;

namespace FalconTimeTracker.Features.Timer;

public static class StartTimer
{
    public record Request(string? Task);

    public static void Map(IEndpointRouteBuilder api) =>
        api.MapPost("/employees/{id:int}/timer/start", async (int id, Request req, AppDb db, TimeProvider clock) =>
        {
            var employee = await db.FindWithRunningTimerAsync(id);
            if (employee is null)
                return Results.NotFound(new { error = "Сотрудник не найден" });

            var entry = employee.StartTimer(req.Task, clock.GetUtcNow().UtcDateTime);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException) // параллельный запрос успел запустить таймер, сработал уникальный индекс
            {
                throw new DomainException(DomainError.Conflict, "Таймер уже запущен");
            }
            return Results.Created($"/api/entries/{entry.Id}", EntryDto.From(entry));
        });
}
