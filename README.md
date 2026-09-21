# Adjudge

Adjudge is a .NET library for typed, probabilistic decisions made from application code. You hand it
a context and get back typed answers carrying probability distributions and calibrated confidence.
No prompts, no JSON, no thresholds, no model names. Questions are closed-set by design (classify over
an enum, rate against ordered levels, or assert a proposition), so the probabilities stay meaningful.
Providers plug in behind one small interface. Jev is the first one.

So far only the shared types exist, in `Adjudge.Abstractions`. There is no builder, no engine and no
provider yet.

## Why

- Answers are typed values (`Classification<T>`, `Rating<T>`, `Assertion`), not JSON you parse yourself.
- The library computes confidence itself, so it means the same thing for every provider, and you can still see where it came from.
- Rubrics live with the enum they describe, so the wording a provider sees is version-controlled alongside the options.

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

## Concepts

Three kinds of question, three kinds of answer:

| Kind | Question | Answer |
|---|---|---|
| Classify | pick one option from an enum | `Classification<T>`: `Value`, `Distribution`, `Confidence` |
| Rate | place the context on an ordered enum | `Rating<T>`: `Value` (fractional position), `Nearest`, `Distribution`, `Confidence` |
| Assert | a yes or no proposition | `Assertion`: `Probability` |

Options and levels are enum members, and the wording that describes them sits on the member:

```csharp
public enum TicketIntent
{
    [Option(
        "Charges, invoices, refunds and payment disputes",
        NotFor = "Questions about where an order is",
        Examples = ["I was charged twice", "My refund never arrived"])]
    Billing,

    [Option("Where an existing order is, or when it arrives")]
    OrderTracking,
}

public enum Urgency
{
    [Level("Can wait several days")]
    Low,

    [Level("Needs attention now")]
    High,
}
```

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
so the engine can check a definition before it calls.

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
