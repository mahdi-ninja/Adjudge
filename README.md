# Adjudge

Adjudge is a .NET library for typed, probabilistic decisions made from application code. You hand it
a context and get back typed answers carrying probability distributions and calibrated confidence.
No prompts, no JSON, no thresholds, no model names. Questions are closed-set by design (classify over
an enum, rate against ordered levels, or assert a proposition), so the probabilities stay meaningful.
Providers plug in behind one small interface, and Jev and any OpenAI-compatible endpoint are built in.

## Why

- Answers are typed values (`Classification<T>`, `Rating<T>`, `Assertion`), not JSON you parse yourself.
- The library computes confidence itself, so it means the same thing for every provider, and you can still see where it came from.
- Rubrics live with the enum they describe, so the wording a provider sees is version-controlled alongside the options.
- You can test any decision without a network, using the fake provider in `Adjudge.Testing`.

## Install

```
dotnet add package Adjudge
dotnet add package Adjudge.Jev
dotnet add package Adjudge.OpenAI
dotnet add package Adjudge.Testing
```

It's not on NuGet yet, so for now reference the projects directly.

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

Registration with dependency injection:

```csharp
services
    .AddAdjudge()
    .AddJev()
    .AddDecision<TicketTriageDecision, TicketContext, TicketTriage>();
```

Or without a container:

```csharp
var provider = new JevProvider(new JevOptions { ApiKey = apiKey });
var engine = new DecisionEngine(provider);
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

There's a runnable version of the quick start in `samples/Adjudge.Sample.Minimal`, and the same decision registered in a host in `samples/Adjudge.Sample.Hosted`.

## Testing

Script a provider and run the real decision:

```csharp
var provider = new FakeDecisionProvider()
    .Classify("intent", nameof(TicketIntent.Billing), 0.92)
    .Rate("urgency", nameof(Urgency.High), 0.7)
    .Assert("abusive", 0.1)
    .WithModel("fake-1");

var decision = new DecisionEngine(provider).Create(new TicketTriageDecision());
var result = await decision.DecideAsync(new TicketContext("I was charged twice", "4471"), ct);

result.Value.Intent.Value.ShouldBe(TicketIntent.Billing);
provider.LastRequest!.Questions.Count.ShouldBe(3);
```

Or skip the engine entirely and fake the decision:

```csharp
var triage = new FakeDecision<TicketContext, TicketTriage>()
    .Returns(new TicketTriage(
        Answers.Classification(TicketIntent.Billing, 0.9),
        Answers.Rating(Urgency.High),
        Answers.Assertion(0.1)));

var routing = await new TicketService(triage).RouteAsync(new TicketContext("...", null), ct);
```

## Providers

Anything implementing `IDecisionProvider` plugs into the engine:

```csharp
public interface IDecisionProvider
{
    string Name { get; }
    DecisionCapabilities Capabilities { get; }
    Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct);
}
```

A `ProviderRequest` carries the serialised `DecisionContext` and the `QuestionSpec` list. A
`ProviderResponse` carries one `AnswerSpec` per question, plus the model and usage. The engine checks
`Capabilities` against the definition before it calls, and does the typing and confidence work itself,
so a provider only has to translate shapes. Register your own with
`services.AddAdjudge().UseProvider<MyProvider>()`.

### Built in

| Package | Provider | Registration | Without a container |
|---|---|---|---|
| `Adjudge.Jev` | Jev, which answers every question in one call and reports its own confidence | `services.AddAdjudge().AddJev()` | `new JevProvider(new JevOptions())` |
| `Adjudge.OpenAI` | Any OpenAI-compatible Chat Completions endpoint: openai.com, Azure OpenAI v1, Ollama | `services.AddAdjudge().AddOpenAI()` | `new OpenAIProvider(new OpenAIOptions())` |

The OpenAI provider asks one chat call per question, labels the options so that each label is a single
token, and reads the distribution either from the first answer token's log probabilities or from
repeated samples. Confidence from it is derived from token probabilities or sample agreement, not
calibrated over the option set.

Labels are read leniently. Everything before the first letter or digit is stripped and the run that
follows is the label, so `**A**`, `- A`, `(A)`, `` `A` `` and `"yes"` all parse. A sampled reply is
split into words and the first word that is an offered label wins, so `Answer: A` parses too, at the
price of reading `A customer wants billing` as `A`.

## Configuration

Each provider reads these environment variables when the matching option is not set:

| Variable | Option | Default |
|---|---|---|
| `TYPESAFE_API_KEY` | `JevOptions.ApiKey` | required |
| `TYPESAFE_BASE_URL` | `JevOptions.BaseUrl` | `https://api.typesafe.ai` |
| `TYPESAFE_DEFAULT_MODEL` | `JevOptions.Model` | `jev-latest` |
| `OPENAI_API_KEY` | `OpenAIOptions.ApiKey` | required |
| `OPENAI_BASE_URL` | `OpenAIOptions.BaseUrl` | `https://api.openai.com/v1` |
| `OPENAI_MODEL` | `OpenAIOptions.Model` | required |

`JevOptions.Timeout` defaults to 10 seconds and `JevOptions.MaxRetries` to 2. Retries cover 408, 429
and 5xx responses, with jittered backoff, and they honour `Retry-After`. Set them in code:

```csharp
services.AddAdjudge().AddJev(o => o.Timeout = TimeSpan.FromSeconds(20));
```

A failed call throws `JevException`, which carries `StatusCode`, `IsTransient`, `RetryAfter`,
`RequestId` and `ResponseBody`. `OpenAIException` carries the same fields.

`OpenAIOptions` shares `Timeout` and `MaxRetries`, and adds `Strategy` (`LogProbabilities` or
`Sampling`), `TopLogProbabilities`, `Temperature`, `Samples`, `SamplingTemperature` and
`MaxConcurrentCalls`:

```csharp
services.AddAdjudge().AddOpenAI(o =>
{
    o.Model = "gpt-4o-mini";
    o.Strategy = ConfidenceStrategy.Sampling;
    o.Samples = 7;
});
```

`TopLogProbabilities` defaults to 5 because that is the most Azure OpenAI accepts on its v1 endpoint,
which rejects anything higher with a 400 that no retry will fix; openai.com allows up to 20, so raise
it when you know the endpoint takes it. `Temperature` is a nullable float defaulting to 0, and setting
it to null omits the parameter altogether, which is what the models that reject a temperature need.

An assertion's probability comes from the raw answer masses: `yes / (yes + no)` when both answers came
back, and when only one did, the residual stands in for the other, `yes / (yes + max(0, 1 - yes))`. It
is clamped to 0.001 to 0.999, so a single confident token never reads as certainty.

## Telemetry

Each evaluation starts one activity on the `Adjudge` `ActivitySource`, tagged with the definition id,
the provider and the model, and marked as an error when the call fails. The `Adjudge` `Meter` carries:

- `adjudge.decisions`, a counter of evaluations tagged with the outcome
- `adjudge.tokens.input` and `adjudge.tokens.output`, counters of reported usage
- `adjudge.confidence`, a histogram tagged by definition id, question and provider

Turn the lot off with `services.AddAdjudge(o => o.EnableTelemetry = false)`.

## Design

See [docs/design.md](docs/design.md).

## Status

Early days. Expect the API to move before 1.0.

## Licence

MIT.
