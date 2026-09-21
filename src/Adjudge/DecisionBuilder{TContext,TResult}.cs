using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Adjudge;

/// <summary>Collects the questions of one decision. Each question binds to a member of the result type, and that member's name becomes the question name in camelCase.</summary>
/// <typeparam name="TContext">The facts the decision takes.</typeparam>
/// <typeparam name="TResult">The typed answers the decision produces.</typeparam>
public sealed class DecisionBuilder<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>
{
    private readonly List<QuestionBinding> _bindings = [];

    internal IReadOnlyList<QuestionBinding> Bindings => _bindings;

    /// <summary>Asks for one member of <typeparamref name="T"/>, with rubrics taken from the <see cref="OptionAttribute"/> on each member.</summary>
    /// <typeparam name="T">The enum whose members are the options.</typeparam>
    /// <param name="member">A direct property access on the result type, such as <c>r => r.Intent</c>.</param>
    /// <param name="instructions">The question in plain words.</param>
    /// <exception cref="DecisionDefinitionException">The expression is not a direct member access, the instructions are blank, or the member is already bound.</exception>
    public void Classify<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
        Expression<Func<TResult, Classification<T>>> member,
        string instructions)
        where T : struct, Enum
    {
        var memberName = MemberName(member);
        Add(new ClassifyBinding<T>(memberName, QuestionName(memberName), Instructions(instructions, memberName)));
    }

    /// <summary>Asks where the context sits on <typeparamref name="T"/>, whose members are ordered by ascending underlying value.</summary>
    /// <typeparam name="T">The enum whose members are the levels.</typeparam>
    /// <param name="member">A direct property access on the result type, such as <c>r => r.Urgency</c>.</param>
    /// <param name="instructions">The question in plain words.</param>
    /// <exception cref="DecisionDefinitionException">The expression is not a direct member access, the instructions are blank, or the member is already bound.</exception>
    public void Rate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
        Expression<Func<TResult, Rating<T>>> member,
        string instructions)
        where T : struct, Enum
    {
        var memberName = MemberName(member);
        Add(new RateBinding<T>(memberName, QuestionName(memberName), Instructions(instructions, memberName)));
    }

    /// <summary>Asks a proposition, which comes back as a probability rather than a yes or no.</summary>
    /// <param name="member">A direct property access on the result type, such as <c>r => r.Abusive</c>.</param>
    /// <param name="instructions">The question in plain words.</param>
    /// <exception cref="DecisionDefinitionException">The expression is not a direct member access, the instructions are blank, or the member is already bound.</exception>
    public AssertQuestionBuilder Assert(Expression<Func<TResult, Assertion>> member, string instructions)
    {
        var memberName = MemberName(member);
        var binding = new AssertBinding(memberName, QuestionName(memberName), Instructions(instructions, memberName));
        Add(binding);
        return new AssertQuestionBuilder(binding);
    }

    private static string MemberName<TAnswer>(Expression<Func<TResult, TAnswer>> member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member.Body is not MemberExpression { Expression: ParameterExpression } expression)
        {
            throw new DecisionDefinitionException($"'{member}' does not bind a member of '{typeof(TResult).Name}'.");
        }

        return expression.Member.Name;
    }

    private static string Instructions(string instructions, string memberName)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new DecisionDefinitionException($"Question '{QuestionName(memberName)}' must declare instructions.");
        }

        return instructions;
    }

    private static string QuestionName(string memberName)
    {
        if (char.IsLower(memberName[0]))
        {
            return memberName;
        }

        var characters = memberName.ToCharArray();
        characters[0] = char.ToLowerInvariant(characters[0]);
        return new string(characters);
    }

    private void Add(QuestionBinding binding)
    {
        if (_bindings.Any(b => string.Equals(b.MemberName, binding.MemberName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DecisionDefinitionException($"Member '{binding.MemberName}' of '{typeof(TResult).Name}' is bound more than once.");
        }

        _bindings.Add(binding);
    }
}
