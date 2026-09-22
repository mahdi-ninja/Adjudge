using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.OpenAI.IntegrationTests;

public sealed class LiveDecisionTests
{
    public static bool IsConfigured => LiveApi.IsConfigured;

    [Fact(
        Skip = "Set OPENAI_API_KEY, OPENAI_BASE_URL and OPENAI_MODEL to run the live OpenAI integration tests.",
        SkipUnless = nameof(IsConfigured))]
    public async Task DecideAsync_WhenAskingTheLiveApi_ReturnsAnAnswerForEveryQuestion()
    {
        using var provider = new OpenAIProvider(new OpenAIOptions());
        var state = JsonDocument.Parse(
            """
            {"channel":"email","message":"I was charged twice for order 4821 and nobody has replied in three days."}
            """).RootElement.Clone();
        var request = new ProviderRequest(
            DecisionContext.FromJson(new object(), state),
            [
                new ClassifySpec(
                    "intent",
                    "What does the customer want?",
                    [
                        new OptionSpec("Billing", "Charges, invoices and refunds"),
                        new OptionSpec("Tracking", "Where an order or delivery is"),
                        new OptionSpec("Other", "Anything else"),
                    ]),
                new RateSpec(
                    "urgency",
                    "How urgent is this message?",
                    [
                        new LevelSpec("Low", "Can wait several days"),
                        new LevelSpec("Medium", "Should be handled today"),
                        new LevelSpec("High", "Needs an immediate response"),
                    ]),
                new AssertSpec("abusive", "Is the message abusive?", "Insults or threats", "Frustrated but civil"),
            ],
            "support.ticket-triage");

        var response = await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        response.Model.ShouldNotBeNullOrWhiteSpace();
        response.Answers.Count.ShouldBe(3);

        var intent = response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        intent.Probabilities.Values.Sum().ShouldBe(1, 0.02);
        intent.Confidence.ShouldBeNull();

        var urgency = response.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>();
        urgency.Probabilities.Values.Sum().ShouldBe(1, 0.02);

        response.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBeInRange(0, 1);
    }
}
