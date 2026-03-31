// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

/// <summary>
/// Provides a fluent API for registering pipelines in dependency injection.
/// </summary>
public interface IPipelineRegistrationBuilder
{
    /// <summary>
    /// Registers a packaged pipeline definition source.
    /// </summary>
    /// <typeparam name="TPipelineDefinition">The packaged definition source type.</typeparam>
    /// <returns>The same registration builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddPipelines()
    ///     .WithPipeline&lt;OrderImportPipeline&gt;();
    /// </code>
    /// </example>
    IPipelineRegistrationBuilder WithPipeline<TPipelineDefinition>()
        where TPipelineDefinition : class, IPipelineDefinitionSource;

    /// <summary>
    /// Registers an inline context-aware pipeline definition.
    /// </summary>
    /// <typeparam name="TContext">The execution context type.</typeparam>
    /// <param name="name">The mandatory pipeline name.</param>
    /// <param name="configure">The callback that configures the pipeline definition.</param>
    /// <returns>The same registration builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddPipelines()
    ///     .WithPipeline&lt;OrderImportContext&gt;("order-import", builder => builder.AddStep(() => { }));
    /// </code>
    /// </example>
    IPipelineRegistrationBuilder WithPipeline<TContext>(
        string name,
        Action<IPipelineDefinitionBuilder<TContext>> configure)
        where TContext : PipelineContextBase;

    /// <summary>
    /// Registers all packaged pipeline definitions from the assembly containing the marker type.
    /// </summary>
    /// <typeparam name="TMarker">A marker type from the target assembly.</typeparam>
    /// <returns>The same registration builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddPipelines()
    ///     .WithPipelinesFromAssembly&lt;ModuleAssemblyMarker&gt;();
    /// </code>
    /// </example>
    IPipelineRegistrationBuilder WithPipelinesFromAssembly<TMarker>();

    /// <summary>
    /// Registers all packaged pipeline definitions from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The same registration builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddPipelines()
    ///     .WithPipelinesFromAssemblies(typeof(ModuleAAssemblyMarker).Assembly, typeof(ModuleBAssemblyMarker).Assembly);
    /// </code>
    /// </example>
    IPipelineRegistrationBuilder WithPipelinesFromAssemblies(
        params Assembly[] assemblies);
}
