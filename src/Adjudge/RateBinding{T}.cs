using System.Diagnostics.CodeAnalysis;
using Adjudge.Providers;

namespace Adjudge;

internal sealed class RateBinding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
    string memberName,
    string name,
    string instructions)
    : QuestionBinding(memberName, name, instructions)
    where T : struct, Enum
{
    public override DecisionCapabilities Capability => DecisionCapabilities.Rate;

    public override void Validate()
    {
        var count = EnumMetadata<T>.Count;
        if (count is < 2 or > 10)
        {
            throw new DecisionDefinitionException(
                $"Question '{Name}' rates over '{typeof(T).Name}', which declares {count} members; between 2 and 10 are allowed.");
        }
    }

    public override QuestionSpec CreateSpec()
    {
        var levels = new List<LevelSpec>(EnumMetadata<T>.Count);
        foreach (var member in EnumMetadata<T>.Levels)
        {
            levels.Add(new LevelSpec(member.Name, member.Rubric));
        }

        return new RateSpec(Name, Instructions, levels);
    }

    public override MappedAnswer Map(AnswerSpec answer, string provider)
    {
        if (answer is not RateAnswerSpec rate)
        {
            throw new ProviderResponseException(
                $"Question '{Name}' expects a rating answer but the provider returned '{answer.GetType().Name}'.",
                provider);
        }

        var distribution = AnswerMapping.ToDistribution<T>(rate.Probabilities, Name, provider);
        var confidence = AnswerMapping.Confidence(distribution.Confidence, rate.Confidence);
        return new MappedAnswer(Rating<T>.From(distribution, confidence), confidence.Value);
    }
}
