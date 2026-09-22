# Samples

Four console samples, all triaging the same three support tickets with the same decision, so you can
see the difference between the wiring and nothing else.

| Sample | Shows | Start here if |
|---|---|---|
| [Adjudge.Sample.Minimal](Adjudge.Sample.Minimal) | An engine, a provider and one decision, with no container | You want the shortest path from a context to a typed answer |
| [Adjudge.Sample.Hosted](Adjudge.Sample.Hosted) | `AddAdjudge().AddJev().AddDecision<>()` inside the Generic Host, consumed by an injected service | You are wiring Adjudge into an ASP.NET app or a worker |
| [Adjudge.Sample.OpenAI](Adjudge.Sample.OpenAI) | The same decision on an OpenAI-compatible endpoint, log probabilities by default, and a `sampling` argument for the other strategy | You have an OpenAI, Azure OpenAI or local OpenAI-compatible model rather than Jev |
| [Adjudge.Sample.Cascade](Adjudge.Sample.Cascade) | Rules first, then Jev at a 0.7 confidence floor, then an OpenAI model for the leftovers, with per-question provider attribution | You want to see how providers compose, and where each question gets answered |

## Running

```
dotnet run --project samples/Adjudge.Sample.Minimal
dotnet run --project samples/Adjudge.Sample.Hosted
dotnet run --project samples/Adjudge.Sample.OpenAI
dotnet run --project samples/Adjudge.Sample.OpenAI -- sampling
dotnet run --project samples/Adjudge.Sample.Cascade
```

The first two read your Jev key from `TYPESAFE_API_KEY`, and the OpenAI one reads `OPENAI_API_KEY`,
`OPENAI_BASE_URL` and `OPENAI_MODEL`. The cascade one calls both providers, so it needs both sets:
`TYPESAFE_API_KEY` as well as `OPENAI_API_KEY`, `OPENAI_BASE_URL` and `OPENAI_MODEL`. Each says so and
stops if what it needs is not set.
