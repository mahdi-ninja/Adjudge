namespace Adjudge.Sample.Hosted;

public sealed record TicketContext(string Message, string? OrderId);

public enum TicketIntent
{
    [Option(
        "Charges, invoices, refunds and payment disputes",
        NotFor = "Questions about where an order is",
        Examples = ["I was charged twice", "My refund never arrived"])]
    Billing,

    [Option("Where an existing order is, or when it arrives")]
    OrderTracking,

    [Option("Sign-in, passwords and account recovery")]
    AccountAccess,

    [Option("Anything the other options do not cover")]
    Other,
}

public enum Urgency
{
    [Level("Can wait several days")]
    Low,

    [Level("Should be handled today")]
    Medium,

    [Level("Needs attention now")]
    High,
}

public sealed record TicketTriage(
    Classification<TicketIntent> Intent,
    Rating<Urgency> Urgency,
    Assertion Abusive);

public sealed class TicketTriageDecision : Decision<TicketContext, TicketTriage>
{
    protected override void Define(DecisionBuilder<TicketContext, TicketTriage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Rate(r => r.Urgency, "How urgent is this message?");
        d.Assert(r => r.Abusive, "Is the message abusive?")
            .True("Insults, threats or slurs")
            .False("Frustrated but civil");
    }
}
