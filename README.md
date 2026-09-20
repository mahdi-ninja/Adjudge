# Adjudge

Adjudge is a .NET library for typed, probabilistic decisions made from application code. You hand it
a context and get back typed answers carrying probability distributions and calibrated confidence.
No prompts, no JSON, no thresholds, no model names. Questions are closed-set by design (classify over
an enum, rate against ordered levels, or assert a proposition), so the probabilities stay meaningful.
Providers plug in behind one small interface. Jev is the first one.

Nothing is implemented yet. So far this is the solution layout, the build settings and CI.

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

## Building

```
dotnet build
dotnet test
```

CI runs the same two commands on every push.

## Design

See [docs/design.md](docs/design.md).

## Status

Early days. Nothing works yet.

## Licence

MIT.
