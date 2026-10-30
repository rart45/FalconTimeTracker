using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimeTracker.Domain;

namespace TimeTracker.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TimeEntry> Entries => Set<TimeEntry>();

    /// <summary>Сотрудник вместе с запущенной записью, без остальной истории: этого хватает для правил таймера.</summary>
    public Task<Employee?> FindWithRunningTimerAsync(int id) =>
        Employees.Include(e => e.Entries.Where(x => x.EndedAt == null)).SingleOrDefaultAsync(e => e.Id == id);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Employee>().HasMany(e => e.Entries).WithOne().HasForeignKey(e => e.EmployeeId);

        // Правило «один запущенный таймер» проверяет Employee, а индекс страхует от гонки двух одновременных запросов.
        b.Entity<TimeEntry>().HasIndex(e => e.EmployeeId).IsUnique().HasFilter("EndedAt IS NULL");
        b.Entity<TimeEntry>().HasIndex(e => e.StartedAt);
    }

    // SQLite не хранит DateTimeKind: без конвертера время читается как Unspecified
    // и уходит в JSON без "Z", а браузер принимает его за локальное.
    protected override void ConfigureConventions(ModelConfigurationBuilder b) =>
        b.Properties<DateTime>().HaveConversion<UtcConverter>();

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
