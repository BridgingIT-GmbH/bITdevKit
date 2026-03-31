// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves scoped services for inline pipeline step execution.
/// </summary>
/// <param name="serviceProvider">The scoped service provider.</param>
public class PipelineServiceResolver(IServiceProvider serviceProvider) : IPipelineServiceResolver
{
    /// <inheritdoc />
    public T GetRequiredService<T>()
    {
        return serviceProvider.GetRequiredService<T>();
    }

    /// <inheritdoc />
    public object GetRequiredService(Type serviceType)
    {
        return serviceProvider.GetRequiredService(serviceType);
    }

    /// <inheritdoc />
    public IEnumerable<T> GetServices<T>()
    {
        return serviceProvider.GetServices<T>();
    }

    /// <inheritdoc />
    public IEnumerable<object> GetServices(Type serviceType)
    {
        return serviceProvider.GetServices(serviceType).Cast<object>();
    }

    /// <inheritdoc />
    public object GetService(Type serviceType)
    {
        return serviceProvider.GetService(serviceType);
    }
}

/// <summary>
/// Provides runtime execution services to an inline pipeline step.
/// </summary>
/// <param name="name">The logical step name.</param>
/// <param name="result">The carried pipeline result.</param>
/// <param name="options">The active execution options.</param>
/// <param name="cancellationToken">The pipeline cancellation token.</param>
/// <param name="services">The scoped service resolver.</param>
public class PipelineInlineStepExecution(
    string name,
    Result result,
    PipelineExecutionOptions options,
    CancellationToken cancellationToken,
    IPipelineServiceResolver services) : IPipelineInlineStepExecution
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public Result Result { get; } = result;

    /// <inheritdoc />
    public PipelineExecutionOptions Options { get; } = options;

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <inheritdoc />
    public IPipelineServiceResolver Services { get; } = services;

    /// <inheritdoc />
    public PipelineControl Continue()
    {
        return PipelineControl.Continue(this.Result);
    }

    /// <inheritdoc />
    public PipelineControl Continue(Result result)
    {
        return PipelineControl.Continue(result);
    }

    /// <inheritdoc />
    public PipelineControl Skip(string message = null)
    {
        return PipelineControl.Skip(this.Result, message);
    }

    /// <inheritdoc />
    public PipelineControl Skip(Result result, string message = null)
    {
        return PipelineControl.Skip(result, message);
    }

    /// <inheritdoc />
    public PipelineControl Retry(string message = null)
    {
        return PipelineControl.Retry(this.Result, message);
    }

    /// <inheritdoc />
    public PipelineControl Retry(Result result, string message = null)
    {
        return PipelineControl.Retry(result, message);
    }

    /// <inheritdoc />
    public PipelineControl Break(string message = null)
    {
        return PipelineControl.Break(this.Result, message);
    }

    /// <inheritdoc />
    public PipelineControl Break(Result result, string message = null)
    {
        return PipelineControl.Break(result, message);
    }

    /// <inheritdoc />
    public PipelineControl Terminate(string message = null)
    {
        return PipelineControl.Terminate(this.Result, message);
    }

    /// <inheritdoc />
    public PipelineControl Terminate(Result result, string message = null)
    {
        return PipelineControl.Terminate(result, message);
    }
}

/// <summary>
/// Provides runtime execution services to an inline pipeline step with a typed context.
/// </summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="name">The logical step name.</param>
/// <param name="context">The typed execution context.</param>
/// <param name="result">The carried pipeline result.</param>
/// <param name="options">The active execution options.</param>
/// <param name="cancellationToken">The pipeline cancellation token.</param>
/// <param name="services">The scoped service resolver.</param>
public class PipelineInlineStepExecution<TContext>(
    string name,
    TContext context,
    Result result,
    PipelineExecutionOptions options,
    CancellationToken cancellationToken,
    IPipelineServiceResolver services) : PipelineInlineStepExecution(name, result, options, cancellationToken, services), IPipelineInlineStepExecution<TContext>
    where TContext : PipelineContextBase
{
    /// <inheritdoc />
    public TContext Context { get; } = context;
}
