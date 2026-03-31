// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Adds pipeline services and registration support to dependency injection.
/// </summary>
public static class PipelineServiceCollectionExtensions
{
    /// <summary>
    /// Adds the pipeline feature to the service collection and returns a fluent registration builder.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The pipeline registration builder.</returns>
    /// <example>
    /// <code>
    /// services.AddPipelines()
    ///     .WithPipeline&lt;OrderImportPipeline&gt;()
    ///     .WithPipeline&lt;OrderImportContext&gt;("inventory-inline", builder => builder.AddStep(() => { }));
    /// </code>
    /// </example>
    public static IPipelineRegistrationBuilder AddPipelines(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var state = services.Find<PipelineRegistrationState>()?.ImplementationInstance as PipelineRegistrationState;
        if (state is null)
        {
            state = new PipelineRegistrationState();
            services.AddSingleton(state);
        }

        services.TryAddSingleton<PipelineRegistry>(sp =>
            new PipelineRegistry(sp, sp.GetRequiredService<PipelineRegistrationState>()));
        services.TryAddSingleton<InMemoryPipelineExecutionTracker>();
        services.TryAddSingleton<IPipelineRuntime, PipelineRuntime>();
        services.TryAddSingleton<IPipelineExecutionTracker>(sp => sp.GetRequiredService<InMemoryPipelineExecutionTracker>());
        services.TryAddSingleton<IPipelineFactory, PipelineFactory>();

        return new PipelineRegistrationBuilder(services, state);
    }
}

/// <summary>
/// Registers pipelines into the additive pipeline configuration state.
/// </summary>
/// <param name="services">The target service collection.</param>
/// <param name="state">The shared additive registration state.</param>
public class PipelineRegistrationBuilder(
    IServiceCollection services,
    PipelineRegistrationState state) : IPipelineRegistrationBuilder
{
    /// <inheritdoc />
    public IPipelineRegistrationBuilder WithPipeline<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource
    {
        services.TryAddTransient<TPipelineDefinition>();
        state.AddDefinitionSourceType(typeof(TPipelineDefinition));
        return this;
    }

    /// <inheritdoc />
    public IPipelineRegistrationBuilder WithPipeline<TContext>(
        string name,
        Action<IPipelineDefinitionBuilder<TContext>> configure)
        where TContext : PipelineContextBase
    {
        var builder = new PipelineDefinitionBuilder<TContext>(name);
        configure?.Invoke(builder);
        state.AddDefinition(builder.Build());
        return this;
    }

    /// <inheritdoc />
    public IPipelineRegistrationBuilder WithPipelinesFromAssembly<TMarker>()
    {
        return this.WithPipelinesFromAssemblies(typeof(TMarker).Assembly);
    }

    /// <inheritdoc />
    public IPipelineRegistrationBuilder WithPipelinesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies.SafeNull().Distinct())
        {
            foreach (var type in assembly
                         .GetTypes()
                         .Where(t => !t.IsAbstract &&
                             !t.IsGenericTypeDefinition &&
                             typeof(IPipelineDefinitionSource).IsAssignableFrom(t)))
            {
                services.TryAddTransient(type);
                state.AddDefinitionSourceType(type);
            }
        }

        return this;
    }
}
