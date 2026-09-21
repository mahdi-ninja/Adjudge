using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Adjudge.Jev;

/// <summary>Registers the Jev provider, on either a service collection or the builder <c>AddAdjudge</c> returns.</summary>
public static class JevServiceCollectionExtensions
{
    /// <summary>Registers the provider along with its named client and retry pipeline. Options are validated at startup, not on the first call.</summary>
    /// <param name="services">The collection the provider is registered into.</param>
    /// <param name="configure">Sets the options in code. Unset values still fall back to the environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddJev(this IServiceCollection services, Action<JevOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<JevOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        return Register(services, builder);
    }

    /// <summary>Registers the provider on the Adjudge builder, so registration reads as one chain.</summary>
    /// <param name="builder">The Adjudge builder to register on.</param>
    /// <param name="configure">Sets the options in code. Unset values still fall back to the environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IAdjudgeBuilder AddJev(this IAdjudgeBuilder builder, Action<JevOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddJev(configure);
        return builder;
    }

    private static IServiceCollection Register(IServiceCollection services, OptionsBuilder<JevOptions> builder)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JevOptions>, JevOptionsValidator>());
        builder.ValidateOnStart();

        services.AddHttpClient(JevOptions.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler(
                JevOptions.HttpClientName,
                (pipeline, context) => JevResilience.Configure(
                    pipeline,
                    context.ServiceProvider.GetRequiredService<IOptions<JevOptions>>().Value));

        services.TryAddSingleton<IDecisionProvider>(provider => new JevProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(JevOptions.HttpClientName),
            provider.GetRequiredService<IOptions<JevOptions>>()));

        return services;
    }
}
