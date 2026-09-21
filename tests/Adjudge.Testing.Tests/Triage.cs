namespace Adjudge.Testing.Tests;

public sealed record Triage(Classification<Intent> Intent, Rating<Urgency> Urgency, Assertion Abusive);
