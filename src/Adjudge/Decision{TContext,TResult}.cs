using System.Diagnostics.CodeAnalysis;

namespace Adjudge;

/// <summary>The base for a decision definition. Derive from it and declare the questions in <see cref="Define"/>; a <see cref="DecisionAttribute"/> names it when the type name will not do.</summary>
/// <typeparam name="TContext">The facts the decision takes.</typeparam>
/// <typeparam name="TResult">The typed answers, constructed from a public constructor whose parameter names match the bound members.</typeparam>
public abstract class Decision<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>
{
    /// <summary>Declares the questions. Called once, when the engine creates the decision.</summary>
    /// <param name="d">The builder the questions are declared on.</param>
    protected abstract void Define(DecisionBuilder<TContext, TResult> d);

    internal DecisionBuilder<TContext, TResult> Build()
    {
        var builder = new DecisionBuilder<TContext, TResult>();
        Define(builder);
        return builder;
    }
}
