// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Defines operations for i query behavior.
/// </summary>
public interface IQueryBehavior;

/// <summary>
/// Defines operations for i query behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQueryBehavior<TRequest, TResponse> : IQueryBehavior
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
