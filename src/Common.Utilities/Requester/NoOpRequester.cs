// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fallback requester that logs a warning when no requester is registered.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NoOpRequester"/> class.
/// </remarks>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
public class NoOpRequester(ILoggerFactory loggerFactory = null) : IRequester
{
    private readonly ILogger<NoOpRequester> logger = loggerFactory?.CreateLogger<NoOpRequester>() ?? NullLogger<NoOpRequester>.Instance;

    /// <summary>
    /// Logs a warning and returns a successful result without processing the request.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A successful result indicating the request was not processed.</returns>
    public Task<Result<TValue>> SendAsync<TRequest, TValue>(
        TRequest request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TValue>
    {
        this.logger.LogWarning("{LogKey} no requester available. Request {RequestType} not processed (RequestId={RequestId})", "REQ", request.GetType().Name, request.RequestId);

        return Task.FromResult(Result<TValue>.Success(default));
    }

    /// <summary>
    /// Logs a warning and returns a successful result without processing the request.
    /// </summary>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A successful result indicating the request was not processed.</returns>
    public Task<Result<TValue>> SendAsync<TValue>(
        IRequest<TValue> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogWarning("{LogKey} no requester available. Request {RequestType} not processed (RequestId={RequestId})", "REQ", request.GetType().Name, request.RequestId);

        return Task.FromResult(Result<TValue>.Success(default));
    }

    /// <summary>
    /// Logs a warning and returns a successful result without processing the request.
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    public Task<Result<TValue>> SendDynamicAsync<TValue>(IRequest<TValue> request, SendOptions options = null, CancellationToken cancellationToken = default)
    {
        this.logger.LogWarning("{LogKey} no requester available. Request {RequestType} not processed (RequestId={RequestId})", "REQ", request.GetType().Name, request.RequestId);

        return Task.FromResult(Result<TValue>.Success(default));
    }

    /// <summary>
    /// Logs a warning and returns a successful result without processing the request.
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    public Task<Result> SendDynamicAsync(IRequest request, SendOptions options = null, CancellationToken cancellationToken = default)
    {
        this.logger.LogWarning("{LogKey} no requester available. Request {RequestType} not processed (RequestId={RequestId})", "REQ", request.GetType().Name, request.RequestId);

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Returns empty registration information since no actual requester is configured.
    /// </summary>
    /// <returns>An empty registration information object.</returns>
    public RegistrationInformation GetRegistrationInformation()
    {
        return new RegistrationInformation(new Dictionary<string, IReadOnlyList<string>>(), []);
    }
}