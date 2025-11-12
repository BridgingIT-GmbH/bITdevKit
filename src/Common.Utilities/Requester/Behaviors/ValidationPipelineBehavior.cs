// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

/// <summary>
/// A pipeline behavior that validates messages using FluentValidation.
/// </summary>
/// <typeparam name="TRequest">The type of the message (request or notification).</typeparam>
/// <typeparam name="TResponse">The type of the response, implementing <see cref="IResult"/>.</typeparam>
/// <remarks>
/// This behavior validates the message using a registered FluentValidation validator before passing it to the next behavior or handler.
/// If validation fails, it returns a failed result with validation errors wrapped in <see cref="FluentValidationError"/>; otherwise, it proceeds with the pipeline.
/// It runs once per message (not per handler) to avoid redundant validation.
/// </remarks>
/// <example>
/// <code>
/// services.AddRequester()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior<ValidationBehavior<,>>();
///
/// public class MyRequest : RequestBase<Unit>
/// {
///     public string Name { get; set; }
///
///     public class Validator : AbstractValidator<MyRequest>
///     {
///         public Validator()
///         {
///             RuleFor(x => x.Name).NotEmpty();
///         }
///     }
/// }
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> class.
/// </remarks>
/// <param name="loggerFactory">The logger factory for creating loggers.</param>
/// <param name="validators">The collection of validators for the message type.</param>
public class ValidationPipelineBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IEnumerable<IValidator<TRequest>> validators = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IValidator<TRequest>[] validators = validators?.ToArray();

    /// <summary>
    /// Indicates whether the behavior can process the specified message.
    /// </summary>
    /// <param name="request">The message to process.</param>
    /// <param name="handlerType">The type of the handler, if applicable.</param>
    /// <returns><c>true</c> if there are validators to apply; otherwise, <c>false</c>.</returns>
    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return this.validators.SafeAny();
    }

    /// <summary>
    /// Validates the message using FluentValidation and proceeds if validation passes.
    /// </summary>
    /// <param name="request">The message to process.</param>
    /// <param name="handlerType">The type of the handler, if applicable.</param>
    /// <param name="next">The delegate to invoke the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the processing, returning a <see cref="TResponse"/>.</returns>
    protected override async Task<TResponse> Process(TRequest request, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(this.validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var errors = validationResults
            .Where(r => !r.IsValid)
            .Select(r => new FluentValidationError(r)).ToList();

        if (errors.Count != 0)
        {
            // Determine the type of TResponse and create the appropriate failure result
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(IResult<>))
            {
                // TResponse is Result<TValue>
                var valueType = responseType.GetGenericArguments()[0];
                var resultType = typeof(Result<>).MakeGenericType(valueType);
                var failureMethod = resultType.GetMethod(nameof(Result<object>.Failure), [typeof(IEnumerable<string>), typeof(IEnumerable<IResultError>)]);
                var failureResult = failureMethod.Invoke(null, [null, errors]);

                return (TResponse)failureResult;
            }
            else if (responseType == typeof(IResult))
            {
                // TResponse is Result (non-generic, for notifications)
                return (TResponse)(object)Result.Failure().WithErrors(errors);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported response type '{responseType}' in ValidationBehavior.");
            }
        }

        return await next();
    }

    /// <summary>
    /// Indicates that this behavior should run once per message, not per handler.
    /// </summary>
    /// <returns><c>false</c> to indicate this is not a handler-specific behavior.</returns>
    public override bool IsHandlerSpecific()
    {
        return false;
    }
}