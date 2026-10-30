namespace FalconTimeTracker.Domain;

public enum DomainError
{
    Invalid,
    Conflict
}

public class DomainException(DomainError error, string message) : Exception(message)
{
    public DomainError Error { get; } = error;

    internal static string RequiredText(string? value, int maxLength, string what)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            throw new DomainException(DomainError.Invalid, $"Напишите {what}, до {maxLength} символов");
        return text;
    }
}
