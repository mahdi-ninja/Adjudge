namespace Adjudge.Sample.Hosted;

public sealed class TicketService(IDecision<TicketContext, TicketTriage> triage)
{
    public async Task<(string Routing, DecisionResult<TicketTriage> Result)> TriageAsync(
        TicketContext ticket,
        CancellationToken ct = default)
    {
        var result = await triage.DecideAsync(ticket, ct).ConfigureAwait(false);
        var answer = result.Value;

        return (Route(answer), result);
    }

    private static string Route(TicketTriage answer)
    {
        if (answer.Abusive.Probability >= 0.7)
        {
            return "quarantine";
        }

        var confidence = answer.Intent.Confidence.Value;
        return confidence switch
        {
            >= 0.85 => $"auto:{answer.Intent.Value}",
            >= 0.5 => $"review:{answer.Intent.Value}",
            _ => "human",
        };
    }
}
