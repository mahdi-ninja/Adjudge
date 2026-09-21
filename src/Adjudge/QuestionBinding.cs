using Adjudge.Providers;

namespace Adjudge;

internal abstract class QuestionBinding(string memberName, string name, string instructions)
{
    public string MemberName { get; } = memberName;

    public string Name { get; } = name;

    public string Instructions { get; } = instructions;

    public abstract DecisionCapabilities Capability { get; }

    public abstract QuestionSpec CreateSpec();

    public abstract MappedAnswer Map(AnswerSpec answer, string provider);

    public abstract void Validate();
}
