using System.Globalization;
using Adjudge.Providers;

namespace Adjudge;

internal sealed class AssertBinding(string memberName, string name, string instructions)
    : QuestionBinding(memberName, name, instructions)
{
    public string? TrueMeans { get; set; }

    public string? FalseMeans { get; set; }

    public override DecisionCapabilities Capability => DecisionCapabilities.Assert;

    public override void Validate()
    {
    }

    public override QuestionSpec CreateSpec() => new AssertSpec(Name, Instructions, TrueMeans, FalseMeans);

    public override MappedAnswer Map(AnswerSpec answer, string provider)
    {
        if (answer is not AssertAnswerSpec assert)
        {
            throw new ProviderResponseException(
                $"Question '{Name}' expects an assertion answer but the provider returned '{answer.GetType().Name}'.",
                provider);
        }

        if (double.IsNaN(assert.Probability) || assert.Probability is < 0 or > 1)
        {
            throw new ProviderResponseException(
                $"Question '{Name}' came back with probability {assert.Probability.ToString(CultureInfo.InvariantCulture)}, which is outside 0 to 1.",
                provider);
        }

        return new MappedAnswer(new Assertion(assert.Probability), null);
    }
}
