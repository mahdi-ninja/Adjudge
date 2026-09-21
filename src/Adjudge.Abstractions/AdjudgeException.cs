namespace Adjudge;

/// <summary>The base of every exception the library raises, so one catch can cover all of them.</summary>
public class AdjudgeException : Exception
{
    /// <summary>Creates the exception with a default message supplied by the runtime.</summary>
    public AdjudgeException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public AdjudgeException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the failure that caused it.</summary>
    public AdjudgeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
