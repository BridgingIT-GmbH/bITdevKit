// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using Humanizer;
using Polly;
using Polly.Retry;

public class RetryDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    public RetryDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        RetryDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RetryDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<RetryDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.Options = options ?? new RetryDocumentStoreClientBehaviorOptions();

        if (this.Options.Attempts <= 0)
        {
            this.Options.Attempts = 1;
        }
    }

    protected ILogger<RetryDocumentStoreClientBehavior<T>> Logger { get; }

    protected IDocumentStoreClient<T> Inner { get; }

    protected RetryDocumentStoreClientBehaviorOptions Options { get; }

    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.DeleteAsync(documentKey, cancellationToken),
                cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(documentKey, cancellationToken),
                cancellationToken)).Result;
    }

    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.FindAsync(documentKey, filter, cancellationToken),
                cancellationToken)).Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.ListAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ListAsync(documentKey, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.ListAsync(documentKey, filter, cancellationToken),
                cancellationToken)).Result;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
                .ExecuteAndCaptureAsync(async context => await this.Inner.CountAsync(cancellationToken),
                    cancellationToken))
            .Result;
    }

    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return (await this.PolicyFactory(this.Options)
            .ExecuteAndCaptureAsync(async context => await this.Inner.ExistsAsync(documentKey, cancellationToken),
                cancellationToken)).Result;
    }

    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.UpsertAsync(documentKey, entity, cancellationToken),
                cancellationToken);
    }

    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.PolicyFactory(this.Options)
            .ExecuteAsync(async context => await this.Inner.UpsertAsync(entities, cancellationToken),
                cancellationToken);
    }

    private AsyncRetryPolicy PolicyFactory(RetryDocumentStoreClientBehaviorOptions options)
    {
        var attempts = 1;
        if (!options.BackoffExponential)
        {
            return Policy.Handle<Exception>()
                .WaitAndRetryAsync(options.Attempts,
                    attempt => TimeSpan.FromMilliseconds(options.Backoff != default ? options.Backoff.Milliseconds : 0),
                    (ex, wait) =>
                    {
                        Activity.Current?.AddEvent(
                            new ActivityEvent($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                        this.Logger.LogError(ex,
                            $"{{LogKey}} behavior retry (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}",
                            Constants.LogKey);
                        attempts++;
                    });
        }

        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(options.Attempts,
                attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                    ? options.Backoff.Milliseconds
                    : 0 * Math.Pow(2, attempt)),
                (ex, wait) =>
                {
                    Activity.Current?.AddEvent(
                        new ActivityEvent($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                    this.Logger.LogError(ex,
                        $"{{LogKey}} behavior retry (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}",
                        Constants.LogKey);
                    attempts++;
                });
    }
}