using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.Tests.Core;

public static class CascadeFixtures
{
    public static readonly ClassifySpec IntentQuestion = new(
        "intent",
        "What does the customer want?",
        [new OptionSpec("Billing", "Charges"), new OptionSpec("Tracking", "Where is it"), new OptionSpec("Returns", "Send it back")]);

    public static readonly RateSpec UrgencyQuestion = new(
        "urgency",
        "How urgent is this?",
        [new LevelSpec("Low", "Days"), new LevelSpec("Medium", "Hours"), new LevelSpec("High", "Now")]);

    public static readonly AssertSpec AbusiveQuestion = new("abusive", "Is the message abusive?");

    public static ProviderRequest Request(params QuestionSpec[] questions) =>
        new(DecisionContext.FromJson(new Ticket("I was charged twice"), Json), questions, "test.cascade");

    private static JsonElement Json { get; } = JsonDocument.Parse("""{"message":"I was charged twice"}""").RootElement.Clone();
}
