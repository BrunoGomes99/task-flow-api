namespace TaskFlow.Application.Common.Exceptions;

/// <summary>
/// Thrown when an expected business rule is violated (e.g. invalid state transition).
/// This is a client-visible error that should not be treated as a server bug.
/// </summary>
public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}

