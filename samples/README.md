# Samples

Two console samples, both triaging the same three support tickets with the same decision, so you can
see the difference between the wiring and nothing else.

| Sample | Shows | Start here if |
|---|---|---|
| [Adjudge.Sample.Minimal](Adjudge.Sample.Minimal) | An engine, a provider and one decision, with no container | You want the shortest path from a context to a typed answer |
| [Adjudge.Sample.Hosted](Adjudge.Sample.Hosted) | `AddAdjudge().AddJev().AddDecision<>()` inside the Generic Host, consumed by an injected service | You are wiring Adjudge into an ASP.NET app or a worker |

## Running

```
dotnet run --project samples/Adjudge.Sample.Minimal
dotnet run --project samples/Adjudge.Sample.Hosted
```

Both read your Jev key from `TYPESAFE_API_KEY`, and say so and stop if it is not set.
