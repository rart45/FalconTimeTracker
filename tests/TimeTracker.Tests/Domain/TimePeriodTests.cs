using TimeTracker.Domain;

namespace TimeTracker.Tests.Domain;

public class TimePeriodTests
{
    private static readonly DateTime Day30 = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimePeriod Oct30 = new(Day30, Day30.AddDays(1));

    private static TimeEntry Entry(DateTime start, DateTime? end = null)
    {
        var employee = new Employee("Иван Тестов");
        var entry = employee.StartTimer("Задача", start);
        if (end is { } e)
            employee.StopTimer(e);
        return entry;
    }

    [Fact]
    public void Entry_inside_period_counts_fully()
    {
        var entry = Entry(Day30.AddHours(9), Day30.AddHours(10).AddMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(90), Oct30.Overlap(entry, now: Day30.AddHours(12)));
    }

    [Fact]
    public void Entry_crossing_end_of_period_is_clipped()
    {
        var entry = Entry(Day30.AddHours(23), Day30.AddHours(25));

        Assert.Equal(TimeSpan.FromHours(1), Oct30.Overlap(entry, now: Day30.AddDays(2)));
        Assert.Equal(TimeSpan.FromHours(1), new TimePeriod(Day30.AddDays(1), Day30.AddDays(2)).Overlap(entry, now: Day30.AddDays(2)));
    }

    [Fact]
    public void Running_entry_counts_until_now()
    {
        var entry = Entry(Day30.AddHours(9));
        Assert.Equal(TimeSpan.FromMinutes(20), Oct30.Overlap(entry, now: Day30.AddHours(9).AddMinutes(20)));
    }

    [Fact]
    public void Entry_outside_period_counts_zero()
    {
        var entry = Entry(Day30.AddDays(-1), Day30.AddDays(-1).AddHours(1));
        Assert.Equal(TimeSpan.Zero, Oct30.Overlap(entry, now: Day30.AddHours(12)));
    }

    [Fact]
    public void Inverted_or_empty_period_is_invalid()
    {
        Assert.Throws<DomainException>(() => new TimePeriod(Day30.AddDays(1), Day30));
        Assert.Throws<DomainException>(() => new TimePeriod(Day30, Day30));
    }
}
