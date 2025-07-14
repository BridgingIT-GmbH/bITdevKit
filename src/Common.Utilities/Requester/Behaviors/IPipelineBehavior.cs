// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Defines a pipeline behavior for processing requests or notifications.
/// </summary>
/// <typeparam name="TRequest">The type of the request or notification.</typeparam>
/// <typeparam name="TResponse">The type of the response, typically <see cref="Result{TValue}"/> for requests or <see cref="Result{Unit}"/> for notifications.</typeparam>
/// <remarks>
/// This interface is implemented by behavior classes to provide cross-cutting concerns (e.g., validation, retry, timeout)
/// in the request or notification processing pipeline. Behaviors can access options (<see cref="SendOptions"/> for requests,
/// <see cref="PublishOptions"/> for notifications) and call the next behavior or handler in the pipeline.
/// </remarks>
/// <example>
/// <code>
/// public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
///     where TRequest : class
///     where TResponse : Result
/// {
///     public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken)
///     {
///         // Validation logic
///         return await next();
///     }
/// }
/// </code>
/// </example>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    /// <summary>
    /// Processes a request or notification, calling the next behavior or handler in the pipeline.
    /// </summary>
    /// <param name="request">The request or notification to process.</param>
    /// <param name="options">The options for request (<see cref="SendOptions"/>) or notification (<see cref="PublishOptions"/>) processing.</param>
    /// <param name="next">The delegate to call the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the operation, returning a <see cref="TResponse"/>.</returns>
    Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the behavior should be applied per handler.
    /// </summary>
    /// <returns><c>true</c> if the behavior is handler-specific (e.g., retry, timeout); <c>false</c> if it should run once per message (e.g., validation).</returns>
    bool IsHandlerSpecific();
}
