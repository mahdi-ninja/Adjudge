using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.Testing.Tests;

public static class Requests
{
    public static ProviderRequest For(params QuestionSpec[] questions) =>
        new(DecisionContext.FromJson(new Ticket("hello"), default), questions, "test.decision");

    public static ClassifySpec Classify(string name, params string[] keys) =>
        new(name, "pick one", [.. keys.Select(k => new OptionSpec(k, k))]);

    public static RateSpec Rate(string name, params string[] keys) =>
        new(name, "place it", [.. keys.Select(k => new LevelSpec(k, k))]);

    public static AssertSpec Assert(string name) => new(name, "is it so?");
}
