using System.Diagnostics.CodeAnalysis;
using Adjudge.Providers;

namespace Adjudge;

internal sealed class ClassifyBinding<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
    string memberName,
    string name,
    string instructions)
    : QuestionBinding(memberName, name, instructions)
    where T : struct, Enum
{
    public override DecisionCapabilities Capability => DecisionCapabilities.Classify;

    public override void Validate()
    {
        var count = EnumMetadata<T>.Count;
        if (count == 0)
        {
            throw new DecisionDefinitionException($"Question '{Name}' classifies over '{typeof(T).Name}', which declares no members.");
        }

        if (count > 255)
        {
            throw new DecisionDefinitionException(
                $"Question '{Name}' classifies over '{typeof(T).Name}', which declares {count} members; at most 255 are allowed.");
        }
    }

    public override QuestionSpec CreateSpec()
    {
        var options = new List<OptionSpec>(EnumMetadata<T>.Count);
        foreach (var member in EnumMetadata<T>.Options)
        {
            options.Add(new OptionSpec(member.Name, member.Rubric));
        }

        return new ClassifySpec(Name, Instructions, options);
    }

    public override MappedAnswer Map(AnswerSpec answer, string provider)
    {
        if (answer is not ClassifyAnswerSpec classify)
        {
            throw new ProviderResponseException(
                $"Question '{Name}' expects a classification answer but the provider returned '{answer.GetType().Name}'.",
                provider);
        }

        var distribution = AnswerMapping.ToDistribution<T>(classify.Probabilities, Name, provider);
        var confidence = AnswerMapping.Confidence(distribution.Confidence, classify.Confidence, classify.Source);
        return new MappedAnswer(new Classification<T>(distribution.Top, distribution, confidence), confidence.Value);
    }
}
