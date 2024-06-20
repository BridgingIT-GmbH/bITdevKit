// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Timeout;

public class TimeoutDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    public TimeoutDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        TimeoutDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<TimeoutDocumentStoreClientBehavior<T>>() ?? NullLoggerFactory.Instance.CreateLogger<TimeoutDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new();
    }

    protected ILogger<TimeoutDocumentStoreClientBehavior<T>> Logger { get; }

    protected TimeoutDocumentStoreClientBehaviorOptions Options { get; }

    protected IDocumentStoreClient<T> Inner { get; }

    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async (context) => await this.Inner.DeleteAsync(documentKey, cancellationToken), cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.FindAsync(cancellationToken), cancellationToken)).Result;
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.FindAsync(documentKey, cancellationToken), cancellationToken)).Result;
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.FindAsync(documentKey, filter, cancellationToken), cancellationToken)).Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.ListAsync(cancellationToken), cancellationToken)).Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.ListAsync(documentKey, filter, cancellationToken), cancellationToken)).Result;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.CountAsync(cancellationToken), cancellationToken)).Result;
    }

    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async (context) => await this.Inner.ExistsAsync(documentKey, cancellationToken), cancellationToken)).Result;
    }

    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async (context) => await this.Inner.UpsertAsync(documentKey, entity, cancellationToken), cancellationToken);
    }

    public async Task UpsertAsync(IEnumerable<(DocumentKey DocumentKey, T Entity)> entities, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async (context) => await this.Inner.UpsertAsync(entities, cancellationToken), cancellationToken);
    }

    private AsyncTimeoutPolicy PolicyFactory(TimeoutDocumentStoreClientBehaviorOptions options)
    {
        return Policy
            .TimeoutAsync(options.Timeout, TimeoutStrategy.Pessimistic, onTimeoutAsync: async (context, timeout, task) =>
                await Task.Run(() => this.Logger.LogError($"{{LogKey}} behavior timeout reached (timeout={timeout.Humanize()}, type={this.GetType().Name})", Constants.LogKey)));
    }
}