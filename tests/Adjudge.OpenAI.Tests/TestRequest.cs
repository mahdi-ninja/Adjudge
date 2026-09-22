using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.OpenAI.Tests;

internal static class TestRequest
{
    public static DecisionContext Context(string json = """{"subject":"charged twice","body":"please help"}""") =>
        DecisionContext.FromJson(new object(), JsonDocument.Parse(json).RootElement.Clone());

    public static ClassifySpec Classify() => new(
        "intent",
        "What does the customer want?",
        [
            new OptionSpec("Billing", "Charges, invoices, refunds"),
            new OptionSpec(
                "Tracking",
                new OptionRubric("Where an existing order is", "Billing questions", ["Where is order 4821", "When does it arrive"])),
            new OptionSpec("Returns", "Sending something back"),
        ]);

    public static RateSpec Rate() => new(
        "urgency",
        "How urgent is this?",
        [
            new LevelSpec("Low", "Can wait days"),
            new LevelSpec("Medium", "Should be handled today"),
            new LevelSpec("High", "Needs an immediate response"),
        ]);

    public static AssertSpec Assert(string? trueMeans = "Insults or threats", string? falseMeans = "Frustrated but civil") =>
        new("abusive", "Is the message abusive?", trueMeans, falseMeans);

    public static ProviderRequest For(params QuestionSpec[] questions) =>
        new(Context(), questions, "support.ticket-triage");
}
