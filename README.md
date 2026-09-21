# Adjudge

Adjudge is a .NET library for typed, probabilistic decisions made from application code. You hand it
a context and get back typed answers carrying probability distributions and calibrated confidence.
No prompts, no JSON, no thresholds, no model names. Questions are closed-set by design (classify over
an enum, rate against ordered levels, or assert a proposition), so the probabilities stay meaningful.
Providers plug in behind one small interface. Jev is the first one.

The builder, the engine and the DI registration are in. No provider ships yet, so for now you bring
your own implementation of `IDecisionProvider`.

## Why

- Answers are typed values (`Classification<T>`, `Rating<T>`, `Assertion`), not JSON you parse yourself.
- The library computes confidence itself, so it means the same thing for every provider, and you can still see where it came from.
- Rubrics live with the enum they describe, so the wording a provider sees is version-controlled alongside the options.

## Install

```
dotnet add package Adjudge
```

It's not on NuGet yet, so for now reference the projects directly.

## Layout

| Project | What goes in it |
|---|---|
| `src/Adjudge.Abstractions` | The types a call site and a provider both see: answers, results, the provider contract |
| `src/Adjudge` | The decision builder, the engine and the DI registration |
| `src/Adjudge.Jev` | The Jev provider |
| `src/Adjudge.Testing` | Fakes, so a decision can be tested without a network |
| `samples/Adjudge.Sample.Minimal` | Engine and provider with no container |
| `samples/Adjudge.Sample.Hosted` | The same decision inside the Generic Host |
| `tests/*` | One test project per source project, plus Jev integration tests |

## Quick start

Context, rubrics and the result type:

```csharp
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
```

The decision:

```csharp
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
```

The decision reports under its own type name. Add `[Decision("support.ticket-triage")]` to the class
when you want a stable identifier of your own on results and telemetry.

The call site:

```csharp
public sealed class TicketService(IDecision<TicketContext, TicketTriage> triage)
{
    public async Task<string> RouteAsync(TicketContext ticket, CancellationToken ct = default)
    {
        var result = await triage.DecideAsync(ticket, ct).ConfigureAwait(false);
        var value = result.Value;

        if (value.Abusive.Probability >= 0.7)
        {
            return "quarantine";
        }

        return value.Intent.Confidence.Value switch
        {
            >= 0.85 => $"auto:{value.Intent.Value}",
            >= 0.5 => $"review:{value.Intent.Value}",
            _ => "human",
        };
    }
}
```

Building the engine takes a provider, and none ships yet, so use your own:

```csharp
var engine = new DecisionEngine(new MyProvider());
IDecision<TicketContext, TicketTriage> triage = engine.Create(new TicketTriageDecision());
```

## Native AOT

The default constructor serialises the context by reflection, which a trimmed or ahead-of-time build
warns about. Hand it a serialiser built over your own source-generated context instead:

```csharp
new DecisionEngine(provider, new DecisionEngineOptions(), ContextSerializer.From(AppJsonContext.Default))
```

`services.AddAdjudge(ContextSerializer.From(AppJsonContext.Default))` does the same under dependency
injection.

Registration with dependency injection does the same thing:

```csharp
services
    .AddAdjudge()
    .UseProvider<MyProvider>()
    .AddDecision<TicketTriageDecision, TicketContext, TicketTriage>();
```

## Concepts

Three kinds of question, three kinds of answer:

| Kind | Question | Answer |
|---|---|---|
| Classify | pick one option from an enum | `Classification<T>`: `Value`, `Distribution`, `Confidence` |
| Rate | place the context on an ordered enum | `Rating<T>`: `Value` (fractional position), `Nearest`, `Distribution`, `Confidence` |
| Assert | a yes or no proposition | `Assertion`: `Probability` |

Options and levels are enum members, and the wording that describes them sits on the member with
`[Option]` or `[Level]`, as in the quick start above.

A member with no attribute falls back to its name. Members are ordered by ascending underlying value
everywhere, which is what makes `Rate` positions and tie-breaks predictable.

`Distribution<T>` normalises the probabilities it is given, covers every member of the enum, and
works out `Top`, `Margin` and a `Confidence` from the spread. `Confidence` itself carries a `Value`,
a `ConfidenceSource` (`Derived`, `Native`, `Sampled` or `Heuristic`) and the provider's own number in
`ProviderReported` when there is one, so a call site can tell a derived figure from a reported one.

Every evaluation returns a `DecisionResult<TResult>` with an `Id`, the `DefinitionId`, the provider
name, the model, the typed `Value`, `Usage` and a timestamp.

## Providers

Anything implementing `IDecisionProvider` will plug into the engine:

```csharp
public interface IDecisionProvider
{
    string Name { get; }
    DecisionCapabilities Capabilities { get; }
    Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct);
}
```

A `ProviderRequest` carries the serialised `DecisionContext` and the `QuestionSpec` list
(`ClassifySpec`, `RateSpec`, `AssertSpec`). A `ProviderResponse` carries one `AnswerSpec` per
question, plus the model and usage. `Capabilities` says which question kinds a provider can handle,
so the engine checks `Capabilities` against the definition before it calls, and does the typing and
confidence work itself. A provider only has to translate shapes. Register yours with
`services.AddAdjudge().UseProvider<MyProvider>()`.

## Telemetry

Each evaluation starts one activity on the `Adjudge` `ActivitySource`, tagged with the definition id,
the provider and the model, and marked as an error when the call fails. The `Adjudge` `Meter` carries:

- `adjudge.decisions`, a counter of evaluations tagged with the outcome
- `adjudge.tokens.input` and `adjudge.tokens.output`, counters of reported usage
- `adjudge.confidence`, a histogram tagged by definition id, question and provider

Turn the lot off with `services.AddAdjudge(o => o.EnableTelemetry = false)`.

## Building

```
dotnet build
dotnet test
```

CI runs the same two commands on every push.

## Design

See [docs/design.md](docs/design.md).

## Status

Early days. Expect the API to move before 1.0.

## Licence

MIT.
