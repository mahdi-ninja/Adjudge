using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Adjudge.OpenAI;

/// <summary>Registers the OpenAI provider, on either a service collection or the builder <c>AddAdjudge</c> returns.</summary>
public static class OpenAIServiceCollectionExtensions
{
    /// <summary>Registers the provider along with its named client and retry pipeline. Options are validated at startup, not on the first call.
    /// <para>
    /// The concrete <see cref="OpenAIProvider"/> is registered as a singleton in its own right, so it can be named
    /// as a cascade stage with <c>Stage&lt;OpenAIProvider&gt;()</c>. Both registrations are <c>TryAdd</c>-based, so a
    /// registration the caller made earlier, of either the concrete type or <c>IDecisionProvider</c>, is
    /// left alone.
    /// </para>
    /// </summary>
    /// <param name="services">The collection the provider is registered into.</param>
    /// <param name="configure">Sets the options in code. Unset values still fall back to the environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddOpenAI(this IServiceCollection services, Action<OpenAIOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<OpenAIOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OpenAIOptions>, OpenAIOptionsValidator>());
        builder.ValidateOnStart();

        services.AddHttpClient(OpenAIOptions.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler(
                OpenAIOptions.HttpClientName,
                (pipeline, context) => OpenAIResilience.Configure(
                    pipeline,
                    context.ServiceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value));

        services.TryAddSingleton(provider => new OpenAIProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(OpenAIOptions.HttpClientName),
            provider.GetRequiredService<IOptions<OpenAIOptions>>()));
        services.TryAddSingleton<IDecisionProvider>(provider => provider.GetRequiredService<OpenAIProvider>());

        return services;
    }

    /// <summary>Registers the provider on the Adjudge builder, so registration reads as one chain.</summary>
    /// <param name="builder">The Adjudge builder to register on.</param>
    /// <param name="configure">Sets the options in code. Unset values still fall back to the environment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IAdjudgeBuilder AddOpenAI(this IAdjudgeBuilder builder, Action<OpenAIOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOpenAI(configure);
        return builder;
    }
}
