namespace Adjudge.Jev.Tests;

internal sealed record Ticket(string Message);

internal sealed record Triage(Classification<Intent> Intent, Rating<Urgency> Urgency, Assertion Abusive);

internal enum Intent
{
    [Option("Charges, invoices, refunds", NotFor = "Order tracking", Examples = ["I was charged twice", "Refund never arrived"])]
    Billing,

    [Option("Where an existing order is")]
    Tracking,

    Returns,
}

internal enum Urgency
{
    [Level("Can wait days")]
    Low,

    [Level("Should be handled today")]
    Medium,

    [Level("Needs an immediate response")]
    High,
}

[Decision("support.ticket-triage")]
internal sealed class TriageDecision : Decision<Ticket, Triage>
{
    protected override void Define(DecisionBuilder<Ticket, Triage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Rate(r => r.Urgency, "How urgent is this?");
        d.Assert(r => r.Abusive, "Is the message abusive?")
            .True("Insults or threats")
            .False("Frustrated but civil");
    }
}
