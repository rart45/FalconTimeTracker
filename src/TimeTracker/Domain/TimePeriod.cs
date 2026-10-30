namespace TimeTracker.Domain;

/// <summary>Полуоткрытый период [From, To) в UTC.</summary>
public record TimePeriod
{
    public TimePeriod(DateTime from, DateTime to)
    {
        From = from.ToUniversalTime();
        To = to.ToUniversalTime();
        if (From >= To)
            throw new DomainException(DomainError.Invalid, "Начало периода должно быть раньше конца");
    }

    public DateTime From { get; }
    public DateTime To { get; }

    /// <summary>Сколько из записи попало в период. Запущенная запись считается до <paramref name="now"/>.</summary>
    public TimeSpan Overlap(TimeEntry entry, DateTime now)
    {
        var start = entry.StartedAt > From ? entry.StartedAt : From;
        var end = entry.EndedAt ?? now;
        if (end > To)
            end = To;
        return end > start ? end - start : TimeSpan.Zero;
    }
}
