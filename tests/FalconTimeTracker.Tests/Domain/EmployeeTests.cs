using FalconTimeTracker.Domain;

namespace FalconTimeTracker.Tests.Domain;

public class EmployeeTests
{
    private static readonly DateTime Nine = new(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_and_stop_create_finished_entry()
    {
        var employee = new Employee("Иван Тестов");

        employee.StartTimer("Вёрстка", Nine);
        var entry = employee.StopTimer(Nine.AddHours(1));

        Assert.Equal(Nine.AddHours(1), entry.EndedAt);
        Assert.Null(employee.RunningEntry);
    }

    [Fact]
    public void Second_start_is_conflict()
    {
        var employee = new Employee("Иван Тестов");
        employee.StartTimer("Первая", Nine);

        var ex = Assert.Throws<DomainException>(() => employee.StartTimer("Вторая", Nine.AddMinutes(1)));

        Assert.Equal(DomainError.Conflict, ex.Error);
        Assert.Equal("Первая", Assert.Single(employee.Entries).Task);
    }

    [Fact]
    public void Can_start_again_after_stop()
    {
        var employee = new Employee("Иван Тестов");
        employee.StartTimer("Утро", Nine);
        employee.StopTimer(Nine.AddHours(1));

        employee.StartTimer("День", Nine.AddHours(2));

        Assert.Equal(2, employee.Entries.Count);
        Assert.Equal("День", employee.RunningEntry?.Task);
    }

    [Fact]
    public void Stop_without_running_timer_is_conflict()
    {
        var ex = Assert.Throws<DomainException>(() => new Employee("Иван Тестов").StopTimer(Nine));
        Assert.Equal(DomainError.Conflict, ex.Error);
    }

    [Fact]
    public void Stop_before_start_is_invalid()
    {
        var employee = new Employee("Иван Тестов");
        employee.StartTimer("Задача", Nine);

        var ex = Assert.Throws<DomainException>(() => employee.StopTimer(Nine.AddMinutes(-1)));
        Assert.Equal(DomainError.Invalid, ex.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_name_and_task_are_invalid(string? value)
    {
        Assert.Throws<DomainException>(() => new Employee(value!));
        Assert.Throws<DomainException>(() => new Employee("Иван Тестов").StartTimer(value, Nine));
    }

    [Fact]
    public void Name_and_task_are_trimmed()
    {
        var employee = new Employee("  Иван Тестов ");
        var entry = employee.StartTimer(" Задача  ", Nine);

        Assert.Equal("Иван Тестов", employee.Name);
        Assert.Equal("Задача", entry.Task);
    }
}
