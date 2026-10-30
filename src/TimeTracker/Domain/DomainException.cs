namespace TimeTracker.Domain;

public enum DomainError
{
    Invalid,
    Conflict
}

public class DomainException(DomainError error, string message) : Exception(message)
{
    public DomainError Error { get; } = error;

    internal static string RequiredText(string? value, int maxLength, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            throw new DomainException(DomainError.Invalid, $"{field}: обязательно, не длиннее {maxLength} символов");
        return text;
    }
}
