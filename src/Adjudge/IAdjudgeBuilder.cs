using Microsoft.Extensions.DependencyInjection;

namespace Adjudge;

/// <summary>The handle <c>AddAdjudge</c> returns, so provider and decision registrations can chain off it.</summary>
public interface IAdjudgeBuilder
{
    /// <summary>The collection the engine was registered into, for further registrations.</summary>
    IServiceCollection Services { get; }
}
