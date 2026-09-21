namespace Adjudge;

/// <summary>Thrown when a decision is malformed. It surfaces when the engine creates the decision, not on the first call.</summary>
public sealed class DecisionDefinitionException : AdjudgeException
{
    /// <summary>Creates the exception with a default message supplied by the runtime.</summary>
    public DecisionDefinitionException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public DecisionDefinitionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the failure that caused it.</summary>
    public DecisionDefinitionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
