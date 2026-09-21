using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Adjudge.Providers;

namespace Adjudge;

internal sealed class DecisionDefinition<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>
{
    private readonly ConstructorInfo _constructor;
    private readonly int[] _argumentPositions;

    private DecisionDefinition(
        string id,
        IReadOnlyList<QuestionBinding> bindings,
        ConstructorInfo constructor,
        int[] argumentPositions)
    {
        Id = id;
        Bindings = bindings;
        Questions = [.. bindings.Select(b => b.CreateSpec())];
        RequiredCapabilities = bindings.Aggregate(DecisionCapabilities.None, (acc, b) => acc | b.Capability);
        _constructor = constructor;
        _argumentPositions = argumentPositions;
    }

    public string Id { get; }

    public IReadOnlyList<QuestionBinding> Bindings { get; }

    public IReadOnlyList<QuestionSpec> Questions { get; }

    public DecisionCapabilities RequiredCapabilities { get; }

    public static DecisionDefinition<TContext, TResult> Build(Decision<TContext, TResult> decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var type = decision.GetType();
        var id = type.GetCustomAttribute<DecisionAttribute>()?.Id ?? SimpleName(type);

        var bindings = decision.Build().Bindings;
        if (bindings.Count == 0)
        {
            throw new DecisionDefinitionException($"'{type.Name}' declares no questions.");
        }

        foreach (var binding in bindings)
        {
            binding.Validate();
        }

        var (constructor, positions) = ResolveConstructor(bindings);
        return new DecisionDefinition<TContext, TResult>(id, bindings, constructor, positions);
    }

    private static string SimpleName(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }

    public TResult CreateResult(object[] answers)
    {
        var arguments = new object[answers.Length];
        for (var index = 0; index < answers.Length; index++)
        {
            arguments[_argumentPositions[index]] = answers[index];
        }

        return (TResult)_constructor.Invoke(arguments);
    }

    private static (ConstructorInfo Constructor, int[] Positions) ResolveConstructor(IReadOnlyList<QuestionBinding> bindings)
    {
        var constructors = typeof(TResult).GetConstructors();
        if (constructors.Length == 0)
        {
            throw new DecisionDefinitionException($"'{typeof(TResult).Name}' has no public constructor.");
        }

        foreach (var candidate in constructors)
        {
            var positions = Match(candidate, bindings);
            if (positions is not null)
            {
                return (candidate, positions);
            }
        }

        throw Describe(constructors.OrderByDescending(c => c.GetParameters().Length).First(), bindings);
    }

    private static int[]? Match(ConstructorInfo constructor, IReadOnlyList<QuestionBinding> bindings)
    {
        var parameters = constructor.GetParameters();
        if (parameters.Length != bindings.Count)
        {
            return null;
        }

        var positions = new int[bindings.Count];
        for (var index = 0; index < bindings.Count; index++)
        {
            var position = Array.FindIndex(parameters, p => string.Equals(p.Name, bindings[index].MemberName, StringComparison.OrdinalIgnoreCase));
            if (position < 0)
            {
                return null;
            }

            positions[index] = position;
        }

        return positions;
    }

    private static DecisionDefinitionException Describe(ConstructorInfo constructor, IReadOnlyList<QuestionBinding> bindings)
    {
        var parameters = constructor.GetParameters();

        foreach (var binding in bindings)
        {
            if (!parameters.Any(p => string.Equals(p.Name, binding.MemberName, StringComparison.OrdinalIgnoreCase)))
            {
                return new DecisionDefinitionException(
                    $"Question '{binding.Name}' is bound to '{binding.MemberName}', which matches no constructor parameter of '{typeof(TResult).Name}'.");
            }
        }

        foreach (var parameter in parameters)
        {
            if (!bindings.Any(b => string.Equals(b.MemberName, parameter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return new DecisionDefinitionException(
                    $"Constructor parameter '{parameter.Name}' of '{typeof(TResult).Name}' has no question.");
            }
        }

        return new DecisionDefinitionException($"No public constructor of '{typeof(TResult).Name}' matches the declared questions.");
    }
}
