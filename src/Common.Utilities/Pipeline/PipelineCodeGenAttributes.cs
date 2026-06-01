// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks a packaged pipeline definition class for source-generated pipeline authoring support.
/// </summary>
/// <remarks>
/// The attributed class must be declared as <c>partial</c> and must not declare an explicit base type.
/// Use <c>[Pipeline]</c> for a no-context pipeline or <c>[Pipeline(typeof(TContext))]</c> for a
/// context-aware pipeline, and the generator supplies the appropriate <see cref="PipelineDefinition"/>
/// or <see cref="PipelineDefinition{TContext}"/> base class. The pipeline runtime itself is unchanged;
/// the generator only produces the usual packaged pipeline definition plumbing and generated wrapper
/// step classes.
/// </remarks>
/// <example>
/// <code>
/// [Pipeline(typeof(OrderImportContext))]
/// public partial class OrderImportPipeline
/// {
///     [PipelineStep(10)]
///     public Result Validate(OrderImportContext context, Result result) => result;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class PipelineAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineAttribute"/> class for a no-context packaged pipeline.
    /// </summary>
    public PipelineAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineAttribute"/> class for a context-aware packaged pipeline.
    /// </summary>
    /// <param name="contextType">The declared pipeline context type.</param>
    public PipelineAttribute(Type contextType)
    {
        this.ContextType = contextType;
    }

    /// <summary>
    /// Gets the declared pipeline context type when the generated pipeline is context-aware.
    /// </summary>
    public Type ContextType { get; }
}

/// <summary>
/// Declares a source-generated pipeline step method and its explicit step order.
/// </summary>
/// <remarks>
/// The order is mandatory so generated pipelines do not depend on source-file ordering.
/// If <see cref="Name"/> is not provided, the generator derives the step name from the method
/// name using the same kebab-case conventions as manual pipelines, while also stripping a
/// trailing <c>Async</c> suffix.
/// </remarks>
/// <param name="order">The explicit execution order of the generated step.</param>
/// <example>
/// <code>
/// [PipelineStep(20, Name = "load-orders")]
/// public async Task&lt;Result&gt; LoadAsync(OrderImportContext context, Result result, CancellationToken cancellationToken)
/// {
///     await Task.Delay(10, cancellationToken);
///     return result.WithMessage("Loaded");
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class PipelineStepAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the explicit step order.
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// Gets or sets the optional explicit step name. When omitted, the name is derived from the method name.
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// Adds a hook type to a source-generated packaged pipeline definition.
/// </summary>
/// <param name="hookType">The hook type to add to the generated pipeline definition.</param>
/// <example>
/// <code>
/// [PipelineHook(typeof(PipelineAuditHook))]
/// public partial class OrderImportPipeline
/// {
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class PipelineHookAttribute(Type hookType) : Attribute
{
    /// <summary>
    /// Gets the hook type to add to the generated pipeline definition.
    /// </summary>
    public Type HookType { get; } = hookType;
}

/// <summary>
/// Adds a behavior type to a source-generated packaged pipeline definition.
/// </summary>
/// <param name="behaviorType">The behavior type to add to the generated pipeline definition.</param>
/// <example>
/// <code>
/// [PipelineBehavior(typeof(PipelineTracingBehavior))]
/// [PipelineBehavior(typeof(PipelineTimingBehavior))]
/// public partial class OrderImportPipeline
/// {
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class PipelineBehaviorAttribute(Type behaviorType) : Attribute
{
    /// <summary>
    /// Gets the behavior type to add to the generated pipeline definition.
    /// </summary>
    public Type BehaviorType { get; } = behaviorType;
}
