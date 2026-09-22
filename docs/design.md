# Adjudge design

Adjudge is a .NET library for making typed, probabilistic decisions from application code.
You supply a context (the facts) and get back typed answers with probability distributions
and confidence. Providers (Jev, OpenAI, fakes, later other providers) translate between the
library's question model and their own API. Application code never sees a provider.

## Goals

- Call sites read like ordinary business code: typed results, no JSON, no thresholds, no model names.
- Providers are pluggable behind one small interface. Jev is the first real provider.
- Every decision is identified and observable.
- Confidence has one meaning across providers, and its origin is visible.
- Testable without a network from the first commit.

## Non-goals for v1

- Free-text or extraction answers. Every answer is over a closed set, so probabilities are meaningful.
- Ranking primitives. Repeated `Rate` covers it.
- Provider cascade, caching, calibration, human-in-the-loop.
- Data-driven (YAML/JSON) definitions. Code first; the spec is the serialised form and may be exposed later.

## Primitives

Three question kinds. Each has a request shape and an answer shape.

| Kind | Question | Answer |
|---|---|---|
| Classify | pick one option from an enum `T` | `Classification<T>`: `Value`, `Distribution`, `Confidence` |
| Rate | place the context on an ordered enum `T` | `Rating<T>`: `Value` (fractional position), `Nearest` (`T`), `Distribution`, `Confidence` |
| Assert | yes/no proposition | `Assertion`: `Probability` |

Enum members are ordered by ascending underlying value everywhere (option order sent to providers, level positions for `Rate`, tie-breaks). For an enum declared in ascending order that's the same as declaration order, and it stays trim-safe, which reflecting over declaration order doesn't.

Options and levels are enum members. Their rubric text comes from attributes:

```csharp
[Option("Charges, invoices, refunds", NotFor = "Order tracking", Examples = ["I was charged twice"])]
Billing,

[Level("Can wait days")]
Low,
```

Metadata is read once per enum type and cached. Enum members without an attribute use their name.

## Public surface (Adjudge.Abstractions)

```csharp
public interface IDecision<TContext, TResult>
{
    Task<DecisionResult<TResult>> DecideAsync(TContext context, CancellationToken ct = default);
}

public sealed class DecisionResult<TResult>
{
    Guid Id;                 // unique per evaluation
    string DefinitionId;     // from [Decision], or the decision's type name
    string Provider;         // provider name
    string? Model;           // provider-reported model version
    TResult Value;           // the typed answers
    Usage? Usage;
    DateTimeOffset Timestamp;
    IReadOnlyDictionary<string, string> Metadata;   // provider detail, never null
}

public readonly record struct Distribution<T> where T : struct, Enum
{
    IReadOnlyDictionary<T, double> Probabilities;
    T Top;                 // highest probability
    double Margin;         // top minus second
    double Confidence;     // library formula, see below
}

public sealed record Classification<T>(T Value, Distribution<T> Distribution, Confidence Confidence);
public sealed record Rating<T>(double Value, T Nearest, Distribution<T> Distribution, Confidence Confidence);
public sealed record Assertion(double Probability);

public readonly record struct Confidence(double Value, ConfidenceSource Source, double? ProviderReported);
public enum ConfidenceSource { Derived, Native, Sampled, Heuristic }
```

### Confidence

`Confidence.Value` is always computed by the library from the distribution so it means the same
thing for every provider. Formula for `n` options with top probability `p`:
`(n * p - 1) / (n - 1)`, clamped to [0, 1]. It hits 1 when all the mass is on one option and 0 when
the distribution is uniform. If the provider reports its own confidence, that's kept in
`ProviderReported`. `Source` says where that reported figure came from: an answer can carry an
explicit `ConfidenceSource` hint, which the engine honours as given; with no hint, `Source` is
`Native` when a confidence was reported and `Derived` when none was.

## Definitions (Adjudge)

```csharp
[Decision("support.ticket-triage")]
public sealed class TicketTriageDecision : Decision<TicketContext, TicketTriage>
{
    protected override void Define(DecisionBuilder<TicketContext, TicketTriage> d)
    {
        d.Classify(r => r.Intent, "What does the customer want?");
        d.Rate(r => r.Urgency, "How urgent is this?");
        d.Assert(r => r.Abusive, "Is the message abusive?").True("Insults or threats").False("Frustrated but civil");
    }
}
```

- `[Decision]` is optional. Without it the definition id is the decision class's own simple type name,
  with no namespace and any generic arity stripped, so `TicketTriageDecision` reports as
  `TicketTriageDecision`. Duplicate question names, unbound result members, or a result member with no
  question all throw `DecisionDefinitionException` when the engine creates the decision, not on the
  first call.
- Result types are records or classes with a public constructor whose parameter names match the
  bound members (case-insensitive). Construction is by that constructor. Reflection is done once
  and cached.
- Question names are the bound member names in camelCase, so a provider sees `intent`, `urgency`, `abusive`.
  camelCase here lowercases the first character only, so `IDNumber` becomes `iDNumber`.
- Instructions and true/false criteria are strings. Option and level rubrics come from `[Option]` and
  `[Level]` alone, and are either a `string` or an `OptionRubric`.
- `OptionRubric` (description, not-for, examples) lives in Adjudge.Abstractions, because providers have to
  understand it to render an option.

## Provider contract (Adjudge.Abstractions)

The extension seam. Everything provider-facing is structured but untyped. Typing happens in the engine.

```csharp
public interface IDecisionProvider
{
    string Name { get; }
    DecisionCapabilities Capabilities { get; }
    Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct);
}

[Flags] public enum DecisionCapabilities { None = 0, Classify = 1, Rate = 2, Assert = 4, NativeConfidence = 8, Batch = 16 }

public sealed record ProviderRequest(
    DecisionContext Context,          // wraps the user object; providers serialise it
    IReadOnlyList<QuestionSpec> Questions,
    string DefinitionId);

public abstract record QuestionSpec(string Name, string Instructions);
public sealed record ClassifySpec(string Name, string Instructions, IReadOnlyList<OptionSpec> Options) : QuestionSpec;
public sealed record RateSpec(string Name, string Instructions, IReadOnlyList<LevelSpec> Levels) : QuestionSpec;
public sealed record AssertSpec(string Name, string Instructions, string? TrueMeans, string? FalseMeans) : QuestionSpec;
public sealed record OptionSpec(string Key, object Rubric);   // string or OptionRubric
public sealed record LevelSpec(string Key, object Rubric);    // string or OptionRubric

public sealed record ProviderResponse(
    IReadOnlyDictionary<string, AnswerSpec> Answers,
    string? Model, Usage? Usage, IReadOnlyDictionary<string, string>? Metadata);

public abstract record AnswerSpec(string Name);
public sealed record ClassifyAnswerSpec(string Name, IReadOnlyDictionary<string, double> Probabilities, double? Confidence, ConfidenceSource? Source) : AnswerSpec;
public sealed record RateAnswerSpec(string Name, IReadOnlyDictionary<string, double> Probabilities, double? Confidence, ConfidenceSource? Source) : AnswerSpec;
public sealed record AssertAnswerSpec(string Name, double Probability) : AnswerSpec;

public sealed record Usage(long? InputTokens, long? OutputTokens);
```

`DecisionContext` holds the original object and a lazily produced `JsonElement` using the
`JsonSerializerOptions` configured on the engine. Providers call `context.AsJson()`. When the JSON is
already in hand, `DecisionContext.FromJson(value, json)` skips serialisation altogether.

Enum keys on the wire are the member names as declared. The engine maps back by name.
If a provider returns an unknown key, misses a question, or hands back probabilities that don't sum
to roughly 1 (tolerance 0.02), you get `ProviderResponseException` after normalisation has been tried.

A provider is allowed to leave a question out of `Answers` when it declines to answer it, rather than
inventing a distribution it does not believe. That is not a free pass: the engine treats a missing
answer as `ProviderResponseException`, so a provider handed straight to the engine has to answer every
question it was asked. Omission is only useful to a composing provider, such as a cascade that puts the
questions a cheap provider declined to a stronger one and merges the answers before the engine sees
them.

`ClassifyAnswerSpec` and `RateAnswerSpec` can carry a `Source` hint saying where the reported
confidence came from (`Native`, `Sampled` or `Heuristic`); left null, the engine infers `Native` when a
confidence was reported and `Derived` when none was. Whatever a provider puts in `ProviderResponse.Metadata`
reaches the caller unchanged as `DecisionResult<TResult>.Metadata`, which is empty rather than null when
the provider sent nothing.

## Engine (Adjudge)

```csharp
var engine = new DecisionEngine(provider, options?);
IDecision<TicketContext, TicketTriage> triage = engine.Create<TicketTriageDecision, TicketContext, TicketTriage>();
IDecision<TicketContext, TicketTriage> same = engine.Create(new TicketTriageDecision());
```

`Create` names all three type arguments, or takes an instance. A single-type-argument
`Create<TicketTriageDecision>()` isn't expressible, because C# can't infer `TContext` and `TResult`
from the base class of a type argument.

`DecisionEngine` validates a definition once, builds `ProviderRequest`s, calls the provider, and maps
`ProviderResponse` to the typed result. It checks `Capabilities` against the definition before calling
the provider and throws `DecisionDefinitionException` if a required kind is unsupported.

Context serialisation: the engine turns the context into JSON before it builds the request. The
two-argument constructor does that by reflection, using `DecisionEngineOptions.JsonSerializerOptions`,
and carries `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, so a trimmed or ahead-of-time
build sees the warning. The trim-safe form is the third argument, a `SerialiseContext` delegate:

```csharp
public delegate JsonElement SerialiseContext(object context);

var engine = new DecisionEngine(provider, new DecisionEngineOptions(), ContextSerializer.From(AppJsonContext.Default));
```

`ContextSerializer` is the static factory. `From(JsonSerializerContext)` looks the context type up in a
source-generated context on each call, and throws `InvalidOperationException` naming the type when it
is not registered there. `From<T>(JsonTypeInfo<T>)` binds one known context type, and throws when it is
handed anything else. Both are annotation-free, and so is the constructor and the
`AddAdjudge(IServiceCollection, SerialiseContext, Action<DecisionEngineOptions>?)` overload that take
them, which is what keeps the whole path clean under trimming.

Telemetry: one activity per evaluation on the `Adjudge` ActivitySource, tagged with the definition id,
the provider and the model, and set to an error status when the call fails. The `Adjudge` Meter carries
counters for decisions and tokens and a histogram of confidence tagged by definition id, question and
provider. `DecisionEngineOptions.EnableTelemetry` turns the lot off.

DI: `services.AddAdjudge(o => ...)` registers the engine and returns a builder;
`.AddDecision<TicketTriageDecision>()` registers `IDecision<TicketContext, TicketTriage>` as singleton;
`.UseProvider<T>()` or provider packages add `IDecisionProvider`. Exactly one provider in v1.

## Exceptions

- `AdjudgeException` base.
- `DecisionDefinitionException` for builder and validation errors, eager.
- `ProviderException(string Provider, bool IsTransient)` for provider failures; provider packages derive from it.
- `ProviderResponseException` for unmappable responses. It is a sibling of `ProviderException`, deriving
  from `AdjudgeException`, and carries the provider name where one is known.

## Jev provider (Adjudge.Jev)

- `POST {BaseUrl}/v1/systemone`, `Authorization: Bearer`, default base `https://api.typesafe.ai`, default model `jev-latest`.
- Options: `ApiKey` (falls back to `TYPESAFE_API_KEY`), `BaseUrl` (`TYPESAFE_BASE_URL`), `Model` (`TYPESAFE_DEFAULT_MODEL`), `Timeout` default 10s.
- Request: `{ state, model, questions: { name: { type: "choice"|"score"|"noul", instructions, criteria } } }`.
  choice criteria is a map key -> rubric; score criteria is an ordered array of rubrics; noul criteria is `{ true, false }` when supplied.
- Response: `{ model, answers: { name: { type, choice, score, noul, confidence, probabilities, legend } }, usage: { input_tokens, output_tokens } }`.
  Score probabilities and legend are keyed by integer position; map back to level keys by order.
  `choice`, `score` and `legend` are ignored, because the library derives all three from `probabilities`.
- Errors: one `JevException : ProviderException` carrying `StatusCode`, `IsTransient`, `RetryAfter`, `RequestId` and `ResponseBody`. 408, 429, 529 and other 5xx are transient, 429 parsing `Retry-After`; every other status, including 400, 401, 403 and 422, is not. Transport failures and timeouts are transient with no status. A missing API key fails the same way, before any HTTP call. `x-typesafe-request-id` becomes `RequestId`.
- Retries: `Microsoft.Extensions.Http.Resilience` standard handler tuned to 2 retries, 0.5s base, 5s max, jitter, retry on 408/429/5xx/529, honour Retry-After.
- Serialisation: System.Text.Json with a source-generated `JsonSerializerContext`, snake_case wire names.
  Rubrics must be `string` or `OptionRubric`; `OptionRubric` is written as `{ description, not_for, examples }`
  with the empty parts omitted. Anything else is a `DecisionDefinitionException`, which keeps the whole
  path trimming and AOT safe.
- Capabilities: Classify | Rate | Assert | NativeConfidence | Batch.
- Limits enforced client-side with `DecisionDefinitionException`: 2 to 10 levels for Rate, at most 255 options for Classify.
- DI: `.AddJev(Action<JevOptions>)` using `IHttpClientFactory`, on either `IServiceCollection` or the `IAdjudgeBuilder` that `AddAdjudge` returns. Options are validated at startup.

## OpenAI provider (Adjudge.OpenAI)

Talks to any OpenAI-compatible Chat Completions endpoint: openai.com, Azure OpenAI v1
(`https://<resource>.services.ai.azure.com/openai/v1`, where bearer auth works), Ollama, vLLM and the
rest. It is built on the official `OpenAI` NuGet package, with the SDK's own retry policy disabled so
the one `Microsoft.Extensions.Http.Resilience` pipeline the library already uses stays in charge.

- Options: `ApiKey` (falls back to `OPENAI_API_KEY`), `BaseUrl` (`OPENAI_BASE_URL`, default
  `https://api.openai.com/v1`, and the version segment is part of it), `Model` (`OPENAI_MODEL`, no
  default because no two endpoints host the same set). A missing key or a missing model throws
  `OpenAIException` before any HTTP call, and so does an `OPENAI_BASE_URL` that is not an absolute URL,
  with the variable named in the message. `Timeout` defaults to 10 seconds, `MaxRetries` to 2 and
  `MaxConcurrentCalls` to 4. `Strategy` picks `LogProbabilities` or `Sampling`, `TopLogProbabilities`
  defaults to 5 and `Temperature` is a nullable float defaulting to 0.
- Options are validated by `IValidateOptions<OpenAIOptions>` at startup under dependency injection, and
  the constructor that takes an `OpenAIOptions` directly runs the same rules and throws a non-transient
  `OpenAIException` carrying the failures, so neither route can send an option the service will reject.
- Capabilities: Classify | Rate | Assert. Not `Batch`: one chat call answers one question, and the
  questions in a request run concurrently up to `MaxConcurrentCalls`, honouring the cancellation
  token. Not `NativeConfidence`: every answer reports `null` for the figure itself, so `Confidence.Value`
  is always the library's own, but each answer hints at where its distribution came from, so
  `ConfidenceSource` is `Heuristic` under `LogProbabilities` and `Sampled` under `Sampling`.
- Prompt: a system message saying the model is a classifier that answers with a single label and
  nothing else, and a user message ordered question, options, state, label line. The state JSON is
  fenced as ```` ```json ```` under a `State:` heading and the instruction to reply with exactly one
  label is the last thing the model reads. Rubrics render as their text when they are a `string`, and
  as the description plus `Not for: ...` and `Examples: ...` lines when they are an `OptionRubric`.
  `TrueMeans` and `FalseMeans` render as the `yes` and `no` lines when the definition supplied them.
  Rubric text and the true and false meanings have their whitespace collapsed to single spaces, so a
  multi-line rubric still renders as one line per label.
- Labels are single tokens, so that first-token log probabilities are meaningful. Classify options are
  labelled `A`, `B`, `C` and so on in the order they were declared, which caps a classification at 26
  options; more throws `DecisionDefinitionException` at request time. Rate levels are labelled `0` to
  `9` in order, so 2 to 10 levels. Assert uses `yes` and `no`.
- Label parsing strips everything before the first letter or digit and then takes the run of letters
  and digits that follows, so `**A**`, `- A`, `(A)`, `` `A` `` and `"yes"` all parse. A log probability
  token is matched that way on its own; a sample is split into whitespace and punctuation delimited
  words and the first word that is an offered label wins, case-insensitively, so `Answer: A` parses.
  A sample that opens with a bare label, as `A customer wants billing` does, is therefore read as that
  label, which is the price of accepting the models that answer in a sentence.
- `ConfidenceStrategy.LogProbabilities` (the default): one call with `max_completion_tokens` 4,
  `top_logprobs` from `TopLogProbabilities` and `temperature` from `Temperature`. The default of 5 is
  the ceiling Azure OpenAI accepts on its v1 endpoint, which rejects anything higher with a
  non-transient 400; openai.com allows up to 20, so the option is validated over 1 to 20 and raising it
  is a per-endpoint choice. `Temperature` is a nullable float: null omits the parameter altogether, for
  the models that reject it. The first content token that is not whitespace is the answer token, so a
  leading newline or space does not cost the answer. Its top log probabilities are trimmed and
  case-normalised, matched against the labels that were offered, turned into `exp(logprob)` and
  renormalised over the labels that matched. The chosen token is folded in when the endpoint left it
  out of its own top list. No match at all is a `ProviderResponseException` carrying the raw content,
  and so is a response with no content log probabilities: when its finish reason is `length`, the
  message says the token budget was consumed before an answer, which is what a reasoning model does,
  and suggests `Sampling` or a different model. These are token probabilities, not a calibrated
  distribution over the option set.
- `ConfidenceStrategy.Sampling`: `Samples` calls (default 5, minimum 2) at `SamplingTemperature`
  (default 1.0) with `max_completion_tokens` 4 and no log probabilities. Each reply's first label is
  parsed and the probabilities are the counts over the samples that parsed. Unparseable samples are
  ignored; all of them unparseable is a `ProviderResponseException`. Samples share the same
  concurrency limit as questions.
- Assert answers are computed from the raw masses rather than from the renormalised distribution:
  `yes / (yes + no)` when both answers are present, and when only one is, the residual stands in for
  the other, `yes / (yes + max(0, 1 - yes))` or its mirror. The result is clamped to 0.001 to 0.999, so
  a single dominant token never reads as certainty.
- Response mapping: `ClassifyAnswerSpec` keyed by option key, `RateAnswerSpec` keyed by level key
  (labels map back by position), `AssertAnswerSpec` with the yes probability. `Model` comes from the
  completion, `Usage` is summed across every call and is null when no completion reported any, and
  `Metadata` carries `strategy` (`log_probabilities` or `sampling`) and `calls`. A question that fails
  propagates its exception and the evaluation fails with it, so the usage the completed calls reported
  is not reported at all.
- Errors: one `OpenAIException : ProviderException` carrying `StatusCode`, `IsTransient`, `RetryAfter`,
  `RequestId` (from `x-request-id`) and `ResponseBody`, mapped from the SDK's `ClientResultException`.
  408, 429 and 5xx are transient, 429 parsing `Retry-After`; every other status, including 400, 401,
  403 and 422, is not. Transport failures and timeouts are transient with no status. A cancelled token
  is never dressed up as a provider failure: the mapping is skipped once cancellation is requested, so
  `OperationCanceledException` reaches the caller.
- HTTP is injected through `OpenAIClientOptions.Transport`, so dependency injection supplies an
  `IHttpClientFactory` client named `Adjudge.OpenAI` and tests supply a recording handler.
- DI: `.AddOpenAI(Action<OpenAIOptions>)` on either `IServiceCollection` or the `IAdjudgeBuilder` that
  `AddAdjudge` returns.

## Testing package (Adjudge.Testing)

- `FakeDecisionProvider`: scripted per question name (`.Classify("intent", top: "Billing", confidence: 0.9)` or full probabilities, each taking an optional `ConfidenceSource` hint), records every `ProviderRequest`, can throw on demand, can omit a question with `.Declines("intent")`, and supports a default answer strategy (uniform) for unscripted questions.
- `FakeDecision<TContext, TResult>`: implements `IDecision<,>` directly, `.Returns(result)`, `.Returns(ctx => result)`, `.Throws(ex)`, records contexts.
- Static factories for answers: `Answers.Classification(T value, double confidence)`, `Answers.Rating(T nearest)`, `Answers.Assertion(double probability)`, each building a plausible distribution.

## Repository layout

```
Adjudge.sln
Directory.Build.props        net8.0;net10.0 for src, net10.0 for tests; nullable, warnings as errors, analyzers, AOT compatible, docs, SourceLink, deterministic
Directory.Packages.props     central package versions
global.json                  .NET 10 SDK
.editorconfig
src/Adjudge.Abstractions
src/Adjudge
src/Adjudge.Jev
src/Adjudge.OpenAI
src/Adjudge.Testing
tests/Adjudge.Tests
tests/Adjudge.Jev.Tests
tests/Adjudge.Jev.IntegrationTests   skipped unless TYPESAFE_API_KEY is set
tests/Adjudge.OpenAI.Tests
tests/Adjudge.OpenAI.IntegrationTests   skipped unless OPENAI_API_KEY, OPENAI_BASE_URL and OPENAI_MODEL are set
tests/Adjudge.Testing.Tests
samples/Adjudge.Sample.Minimal
samples/Adjudge.Sample.Hosted
docs/design.md
```

Test stack: xUnit v3, Shouldly, Verify.Xunit for JSON snapshots, coverlet. No mocking library.
Versioning: MinVer. Licence: MIT.

## Code conventions

- C# 14, file-scoped namespaces, `sealed` by default, records for data, no regions.
- Comments only where the code cannot explain itself. No summary comments that restate the member name. Every public member carries an XML doc that says something the identifier does not.
- Async only. `CancellationToken` on every async method. `ConfigureAwait(false)` in library code.
- Tests: one behaviour per test, names `Method_Scenario_Expectation`, arrange/act/assert without comments marking them.
