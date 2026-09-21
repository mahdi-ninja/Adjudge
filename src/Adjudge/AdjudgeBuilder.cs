using Microsoft.Extensions.DependencyInjection;

namespace Adjudge;

internal sealed class AdjudgeBuilder(IServiceCollection services) : IAdjudgeBuilder
{
    public IServiceCollection Services { get; } = services;
}
