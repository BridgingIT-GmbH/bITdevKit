// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;
using System.Threading;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

public class ChaosDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    public ChaosDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        ChaosDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<ChaosDocumentStoreClientBehavior<T>>() ?? NullLoggerFactory.Instance.CreateLogger<ChaosDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new();
    }

    protected ILogger<ChaosDocumentStoreClientBehavior<T>> Logger { get; }

    protected ChaosDocumentStoreClientBehaviorOptions Options { get; }

    protected IDocumentStoreClient<T> Inner { get; }

    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async (context) => await this.Inner.DeleteAsync(documentKey, cancellationToken), cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.FindAsync(cancellationToken), cancellationToken).Result;
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.FindAsync(documentKey, cancellationToken), cancellationToken).Result;
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.FindAsync(documentKey, filter, cancellationToken), cancellationToken).Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.ListAsync(cancellationToken), cancellationToken).Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.ListAsync(documentKey, filter, cancellationToken), cancellationToken).Result;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.CountAsync(cancellationToken), cancellationToken).Result;
    }

    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.PolicyFactory(this.Options)
            .ExecuteAndCapture(async (context) => await this.Inner.ExistsAsync(documentKey, cancellationToken), cancellationToken).Result;
    }

    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async (context) => await this.Inner.UpsertAsync(documentKey, entity, cancellationToken), cancellationToken);
    }

    public async Task UpsertAsync(IEnumerable<(DocumentKey DocumentKey, T Entity)> entities, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .Execute(async (context) => await this.Inner.UpsertAsync(entities, cancellationToken), cancellationToken);
    }

    private InjectOutcomePolicy PolicyFactory(ChaosDocumentStoreClientBehaviorOptions options)
    {
        return MonkeyPolicy.InjectException(with => // https://github.com/Polly-Contrib/Simmy#Inject-exception
            with.Fault(options.Fault ?? new ChaosException())
                .InjectionRate(options.InjectionRate)
                .Enabled());
    }
}