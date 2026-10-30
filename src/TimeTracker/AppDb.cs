using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TimeTracker;

public class Employee
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class TimeEntry
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public required string Task { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TimeEntry> Entries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TimeEntry>().HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeId);

        // Не больше одного запущенного таймера на сотрудника. Защищает и от гонки двух одновременных запросов.
        b.Entity<TimeEntry>().HasIndex(e => e.EmployeeId).IsUnique().HasFilter("EndedAt IS NULL");
    }

    // SQLite не хранит DateTimeKind: без конвертера время читается как Unspecified
    // и уходит в JSON без "Z", а браузер принимает его за локальное.
    protected override void ConfigureConventions(ModelConfigurationBuilder b) =>
        b.Properties<DateTime>().HaveConversion<UtcConverter>();

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
