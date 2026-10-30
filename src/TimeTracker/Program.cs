using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Domain;
using TimeTracker.Features.Employees;
using TimeTracker.Features.Reports;
using TimeTracker.Features.Timer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>((sp, o) =>
    o.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Db")));
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
{
    try
    {
        return await next(ctx);
    }
    catch (DomainException e)
    {
        var status = e.Error == DomainError.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
        return Results.Json(new { error = e.Message }, statusCode: status);
    }
});

ListEmployees.Map(api);
AddEmployee.Map(api);
StartTimer.Map(api);
StopTimer.Map(api);
GetReport.Map(api);

app.Run();
