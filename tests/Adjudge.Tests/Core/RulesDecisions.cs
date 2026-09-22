namespace Adjudge.Tests.Core;

public sealed record RuledTriage(Classification<Intent> Intent, Assertion Abusive);

[Decision("test.rules-triage")]
public sealed class RuledTriageDecision : Decision<Ticket, RuledTriage>
{
    protected override void Define(DecisionBuilder<Ticket, RuledTriage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Assert(r => r.Abusive, "Is the message abusive?");
    }
}
