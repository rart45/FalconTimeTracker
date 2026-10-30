namespace FalconTimeTracker.Domain;

public class Employee(string name)
{
    private readonly List<TimeEntry> _entries = [];

    public int Id { get; private set; }
    public string Name { get; private set; } = DomainException.RequiredText(name, 100, "имя сотрудника");

    // Чтобы правила работали, запущенная запись должна быть загружена. См. AppDb.FindWithRunningTimerAsync.
    public IReadOnlyCollection<TimeEntry> Entries => _entries;
    public TimeEntry? RunningEntry => _entries.SingleOrDefault(e => e.IsRunning);

    public TimeEntry StartTimer(string? task, DateTime now)
    {
        if (RunningEntry is not null)
            throw new DomainException(DomainError.Conflict, "Таймер уже запущен");

        var entry = new TimeEntry(task, now);
        _entries.Add(entry);
        return entry;
    }

    public TimeEntry StopTimer(DateTime now)
    {
        var entry = RunningEntry ?? throw new DomainException(DomainError.Conflict, "Нет запущенного таймера");
        entry.Stop(now);
        return entry;
    }
}
