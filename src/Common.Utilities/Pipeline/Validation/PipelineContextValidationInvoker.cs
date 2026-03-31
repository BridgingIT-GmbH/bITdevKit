// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Executes FluentValidation validators for pipeline execution contexts.
/// </summary>
public sealed class PipelineContextValidationInvoker : IPipelineContextValidationInvoker
{
    private static readonly ConcurrentDictionary<Type, Func<PipelineContextValidationInvoker, PipelineContextBase, IServiceProvider, CancellationToken, Task<Result>>> Cache = [];

    /// <inheritdoc />
    public Task<Result> ValidateAsync(
        PipelineContextBase context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var executor = Cache.GetOrAdd(
            context.GetType(),
            static contextType => CreateExecutor(contextType));

        return executor(this, context, serviceProvider, cancellationToken);
    }

    private static Func<PipelineContextValidationInvoker, PipelineContextBase, IServiceProvider, CancellationToken, Task<Result>> CreateExecutor(Type contextType)
    {
        var method = typeof(PipelineContextValidationInvoker)
            .GetMethod(nameof(ValidateTypedAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.MakeGenericMethod(contextType)
            ?? throw new InvalidOperationException($"Unable to create pipeline validation executor for '{contextType.PrettyName()}'.");

        return (invoker, context, serviceProvider, cancellationToken) =>
            (Task<Result>)method.Invoke(invoker, [context, serviceProvider, cancellationToken])!;
    }

    private async Task<Result> ValidateTypedAsync<TContext>(
        PipelineContextBase context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TContext : PipelineContextBase
    {
        try
        {
            var validators = serviceProvider.GetServices<IValidator<TContext>>().ToArray();
            if (validators.Length == 0)
            {
                return Result.Success();
            }

            var typedContext = (TContext)context;
            var validationContext = new ValidationContext<TContext>(typedContext);
            var validationResults = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(validationContext, cancellationToken)));
            var failures = validationResults
                .Where(static result => !result.IsValid)
                .SelectMany(static result => result.Errors)
                .Where(static failure => failure is not null)
                .ToList();

            return failures.Count == 0
                ? Result.Success()
                : Result.Failure()
                    .WithError(new FluentValidationError(new ValidationResult(failures)))
                    .WithMessage("Validation failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Validation infrastructure failures should still surface as validation-shaped errors so
            // callers get a consistent result contract instead of a step/runtime exception.
            return Result.Failure()
                .WithError(new FluentValidationError(new ValidationResult(
                [
                    new ValidationFailure(string.Empty, $"Validation execution failed: {ex.GetBaseException().Message}")
                ])))
                .WithMessage("Validation failed");
        }
    }
}
