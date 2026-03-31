// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a packaged source that can build a reusable pipeline definition.
/// </summary>
public interface IPipelineDefinitionSource
{
    /// <summary>
    /// Gets the logical pipeline name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the immutable pipeline definition represented by this source.
    /// </summary>
    /// <returns>The built pipeline definition.</returns>
    /// <example>
    /// <code>
    /// var definition = new OrderImportPipeline().Build();
    /// </code>
    /// </example>
    IPipelineDefinition Build();
}

/// <summary>
/// Represents a packaged source that can build a reusable pipeline definition for a specific context type.
/// </summary>
public interface IPipelineDefinitionSource<TContext> : IPipelineDefinitionSource
    where TContext : PipelineContextBase;

/// <summary>
/// Provides a lightweight base class for packaged no-context pipeline definitions.
/// </summary>
public abstract class PipelineDefinition : IPipelineDefinitionSource
{
    /// <summary>
    /// Gets the pipeline name. By default the class name is converted to kebab-case and a trailing <c>Pipeline</c> suffix is removed.
    /// </summary>
    public virtual string Name => PipelineNameConvention.FromType(this.GetType());

    /// <summary>
    /// Builds the immutable pipeline definition from the packaged definition source.
    /// </summary>
    /// <returns>The built pipeline definition.</returns>
    /// <example>
    /// <code>
    /// public sealed class FileCleanupPipeline : PipelineDefinition
    /// {
    ///     protected override void Configure(IPipelineDefinitionBuilder builder)
    ///     {
    ///         builder.AddStep(() => { });
    ///     }
    /// }
    /// </code>
    /// </example>
    public IPipelineDefinition Build()
    {
        var builder = new PipelineDefinitionBuilder(this.Name);
        this.Configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Configures the pipeline definition through the provided builder.
    /// </summary>
    /// <param name="builder">The builder used to compose the pipeline definition.</param>
    protected abstract void Configure(IPipelineDefinitionBuilder builder);
}

/// <summary>
/// Provides a lightweight base class for packaged context-aware pipeline definitions.
/// </summary>
public abstract class PipelineDefinition<TContext> : IPipelineDefinitionSource<TContext>
    where TContext : PipelineContextBase
{
    /// <summary>
    /// Gets the pipeline name. By default the class name is converted to kebab-case and a trailing <c>Pipeline</c> suffix is removed.
    /// </summary>
    public virtual string Name => PipelineNameConvention.FromType(this.GetType());

    /// <summary>
    /// Builds the immutable pipeline definition from the packaged definition source.
    /// </summary>
    /// <returns>The built pipeline definition.</returns>
    /// <example>
    /// <code>
    /// public sealed class OrderImportPipeline : PipelineDefinition&lt;OrderImportContext&gt;
    /// {
    ///     protected override void Configure(IPipelineDefinitionBuilder&lt;OrderImportContext&gt; builder)
    ///     {
    ///         builder.AddStep&lt;ValidateOrderImportStep&gt;();
    ///     }
    /// }
    /// </code>
    /// </example>
    public IPipelineDefinition Build()
    {
        var builder = new PipelineDefinitionBuilder<TContext>(this.Name);
        this.Configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Configures the pipeline definition through the provided typed builder.
    /// </summary>
    /// <param name="builder">The typed builder used to compose the pipeline definition.</param>
    protected abstract void Configure(IPipelineDefinitionBuilder<TContext> builder);
}
