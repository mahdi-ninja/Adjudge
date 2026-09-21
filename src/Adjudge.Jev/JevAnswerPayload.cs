namespace Adjudge.Jev;

internal sealed class JevAnswerPayload
{
    public string? Type { get; set; }

    public double? Noul { get; set; }

    public double? Confidence { get; set; }

    public Dictionary<string, double>? Probabilities { get; set; }
}
