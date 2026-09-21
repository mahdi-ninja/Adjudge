namespace Adjudge.Jev;

internal sealed class JevResponsePayload
{
    public string? Model { get; set; }

    public Dictionary<string, JevAnswerPayload>? Answers { get; set; }

    public JevUsagePayload? Usage { get; set; }
}
