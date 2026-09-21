namespace Adjudge.Testing.Tests;

[Decision("support.ticket-triage")]
public sealed class TriageDecision : Decision<Ticket, Triage>
{
    protected override void Define(DecisionBuilder<Ticket, Triage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Rate(r => r.Urgency, "How urgent is this?");
        d.Assert(r => r.Abusive, "Is the message abusive?");
    }
}
