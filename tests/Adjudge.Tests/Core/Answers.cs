using Adjudge.Providers;

namespace Adjudge.Tests.Core;

public static class Answers
{
    public static ProviderResponse Triage(
        double? classifyConfidence = null,
        double? rateConfidence = null,
        double abusive = 0.8,
        string? model = "stub-1",
        Usage? usage = null) =>
        new(
            new Dictionary<string, AnswerSpec>
            {
                ["intent"] = new ClassifyAnswerSpec(
                    "intent",
                    new Dictionary<string, double> { ["Billing"] = 0.8, ["Tracking"] = 0.15, ["Returns"] = 0.05 },
                    classifyConfidence),
                ["urgency"] = new RateAnswerSpec(
                    "urgency",
                    new Dictionary<string, double> { ["Low"] = 0.1, ["Medium"] = 0.3, ["High"] = 0.6 },
                    rateConfidence),
                ["abusive"] = new AssertAnswerSpec("abusive", abusive),
            },
            model,
            usage);

    public static ProviderResponse Of(params AnswerSpec[] answers) =>
        new(answers.ToDictionary(a => a.Name));
}
