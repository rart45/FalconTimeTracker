namespace TimeTracker.Domain;

public class TimeEntry(string? task, DateTime startedAt)
{
    public int Id { get; private set; }
    public int EmployeeId { get; private set; }
    public string Task { get; private set; } = DomainException.RequiredText(task, 500, "над чем работает сотрудник");
    public DateTime StartedAt { get; private set; } = startedAt;
    public DateTime? EndedAt { get; private set; }

    public bool IsRunning => EndedAt is null;

    internal void Stop(DateTime now)
    {
        if (now < StartedAt)
            throw new DomainException(DomainError.Invalid, "Время остановки раньше времени старта");
        EndedAt = now;
    }
}
