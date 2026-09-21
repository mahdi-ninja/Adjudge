namespace Adjudge.Tests.Core;

public sealed record Ticket(string Message);

public sealed record Triage(Classification<Intent> Intent, Rating<Urgency> Urgency, Assertion Abusive);

[Decision("support.ticket-triage")]
public sealed class TriageDecision : Decision<Ticket, Triage>
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

public sealed record IntentOnly(Classification<Intent> Intent);

[Decision("test.intent")]
public sealed class IntentOnlyDecision : Decision<Ticket, IntentOnly>
{
    protected override void Define(DecisionBuilder<Ticket, IntentOnly> d) =>
        d.Classify(r => r.Intent, "What does the customer want?");
}

public sealed record UrgencyOnly(Rating<Urgency> Urgency);

[Decision("test.urgency")]
public sealed class UrgencyOnlyDecision : Decision<Ticket, UrgencyOnly>
{
    protected override void Define(DecisionBuilder<Ticket, UrgencyOnly> d) =>
        d.Rate(r => r.Urgency, "How urgent is this?");
}

public sealed record AbusiveOnly(Assertion Abusive);

[Decision("test.abusive")]
public sealed class AbusiveOnlyDecision : Decision<Ticket, AbusiveOnly>
{
    protected override void Define(DecisionBuilder<Ticket, AbusiveOnly> d) =>
        d.Assert(r => r.Abusive, "Is the message abusive?");
}

public sealed record PriorityOnly(Classification<Priority> Priority);

[Decision("test.priority")]
public sealed class PriorityOnlyDecision : Decision<Ticket, PriorityOnly>
{
    protected override void Define(DecisionBuilder<Ticket, PriorityOnly> d) =>
        d.Classify(r => r.Priority, "How should this be prioritised?");
}

public sealed record PriorityRating(Rating<Priority> Priority);

[Decision("test.priority-rating")]
public sealed class PriorityRatingDecision : Decision<Ticket, PriorityRating>
{
    protected override void Define(DecisionBuilder<Ticket, PriorityRating> d) =>
        d.Rate(r => r.Priority, "How pressing is this?");
}

public sealed class UnnamedDecision : Decision<Ticket, IntentOnly>
{
    protected override void Define(DecisionBuilder<Ticket, IntentOnly> d) =>
        d.Classify(r => r.Intent, "What does the customer want?");
}

[Decision("test.duplicate")]
public sealed class DuplicateMemberDecision : Decision<Ticket, IntentOnly>
{
    protected override void Define(DecisionBuilder<Ticket, IntentOnly> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Classify(r => r.Intent, "Asked a second time");
    }
}

public sealed record Unanswered(Classification<Intent> Intent, Assertion Abusive);

[Decision("test.unanswered")]
public sealed class UnansweredParameterDecision : Decision<Ticket, Unanswered>
{
    protected override void Define(DecisionBuilder<Ticket, Unanswered> d) =>
        d.Classify(r => r.Intent, "What does the customer want?");
}

public sealed record Unbound(Classification<Intent> Intent)
{
    public Assertion Extra { get; init; } = new(0.5);
}

[Decision("test.unbound")]
public sealed class UnboundMemberDecision : Decision<Ticket, Unbound>
{
    protected override void Define(DecisionBuilder<Ticket, Unbound> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Assert(r => r.Extra, "Is there anything else?");
    }
}

public sealed record SoloRating(Rating<Solo> Level);

[Decision("test.too-few-levels")]
public sealed class TooFewLevelsDecision : Decision<Ticket, SoloRating>
{
    protected override void Define(DecisionBuilder<Ticket, SoloRating> d) =>
        d.Rate(r => r.Level, "Where does this sit?");
}

public sealed record ElevenRating(Rating<Eleven> Level);

[Decision("test.too-many-levels")]
public sealed class TooManyLevelsDecision : Decision<Ticket, ElevenRating>
{
    protected override void Define(DecisionBuilder<Ticket, ElevenRating> d) =>
        d.Rate(r => r.Level, "Where does this sit?");
}

public sealed record BlankClassification(Classification<Blank> Choice);

[Decision("test.no-options")]
public sealed class NoOptionsDecision : Decision<Ticket, BlankClassification>
{
    protected override void Define(DecisionBuilder<Ticket, BlankClassification> d) =>
        d.Classify(r => r.Choice, "Pick one");
}

public sealed record ManyClassification(Classification<ManyOptions> Choice);

[Decision("test.too-many-options")]
public sealed class TooManyOptionsDecision : Decision<Ticket, ManyClassification>
{
    protected override void Define(DecisionBuilder<Ticket, ManyClassification> d) =>
        d.Classify(r => r.Choice, "Pick one");
}

[Decision("test.telemetry")]
public sealed class TelemetryDecision : Decision<Ticket, Triage>
{
    protected override void Define(DecisionBuilder<Ticket, Triage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Rate(r => r.Urgency, "How urgent is this?");
        d.Assert(r => r.Abusive, "Is the message abusive?");
    }
}
