// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text.Json;

/// <summary>
/// Implements <see cref="IDocumentStoreProvider" /> using an in-memory context.
/// </summary>
/// <remarks>
/// This provider is useful for tests, local development, and other scenarios where persistence is not required beyond the
/// current process.
/// </remarks>
/// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
/// <param name="context">The optional shared in-memory context backing the provider.</param>
/// <param name="options">The optional query safety options.</param>
public class InMemoryDocumentStoreProvider(
    ILoggerFactory loggerFactory,
    InMemoryDocumentStoreContext context = null,
    DocumentStoreOptions options = null) : IDocumentStoreProvider
{
    private const string ProviderName = "in-memory";
    private readonly DocumentStoreOptions options = options ?? new DocumentStoreOptions { AllowFullScans = true };

    /// <summary>
    /// Gets the logger used by the provider.
    /// </summary>
    protected ILogger<InMemoryDocumentStoreProvider> Logger { get; } =
        loggerFactory?.CreateLogger<InMemoryDocumentStoreProvider>() ??
        NullLoggerFactory.Instance.CreateLogger<InMemoryDocumentStoreProvider>();

    /// <summary>
    /// Gets the in-memory context backing the provider.
    /// </summary>
    protected InMemoryDocumentStoreContext Context { get; } = context ?? new InMemoryDocumentStoreContext();

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedEfficiently,
        FullScan = DocumentQuerySupport.SupportedEfficiently,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = true
    };

    /// <inheritdoc />
    public Task<Result<T>> GetResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = this.ValidateExactKey(documentKey);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<T>.Failure(validation));
        }

        var entity = this.Context.Query<T>()
            .FirstOrDefault(e => e.DocumentKey.PartitionKey == documentKey.PartitionKey && e.DocumentKey.RowKey == documentKey.RowKey)
            .Content;

        return Task.FromResult(entity is null
            ? Result<T>.Failure(new DocumentStoreNotFoundError($"Document '{documentKey.PartitionKey}/{documentKey.RowKey}' was not found."))
            : Result<T>.Success(entity.Clone()));
    }

    /// <inheritdoc />
    public Task<Result<DocumentPage<T>>> FindPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = DocumentQueryValidator.ValidatePage<T>("find", ProviderName, query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<DocumentPage<T>>.Failure(validation));
        }

        var rows = this.ApplyQuery(this.Context.Query<T>(), query).ToList();
        rows = this.ApplyContinuation(rows, validation.Value.ContinuationToken?.NativeToken).ToList();

        var pageRows = rows.Take(validation.Value.Take + 1).ToList();
        var items = pageRows.Take(validation.Value.Take).Select(e => e.Content.Clone()).ToList();
        var continuationToken = pageRows.Count > validation.Value.Take
            ? this.CreateContinuationToken(validation.Value.QueryHash, pageRows[validation.Value.Take - 1].DocumentKey)
            : null;

        return Task.FromResult(Result<DocumentPage<T>>.Success(new DocumentPage<T>
        {
            Items = items,
            ContinuationToken = continuationToken
        }));
    }

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = DocumentQueryValidator.ValidatePage<T>("list", ProviderName, query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<DocumentKeyPage>.Failure(validation));
        }

        var rows = this.ApplyQuery(this.Context.Query<T>(), query).ToList();
        rows = this.ApplyContinuation(rows, validation.Value.ContinuationToken?.NativeToken).ToList();

        var pageRows = rows.Take(validation.Value.Take + 1).ToList();
        var keys = pageRows.Take(validation.Value.Take).Select(e => e.DocumentKey).ToList();
        var continuationToken = pageRows.Count > validation.Value.Take
            ? this.CreateContinuationToken(validation.Value.QueryHash, pageRows[validation.Value.Take - 1].DocumentKey)
            : null;

        return Task.FromResult(Result<DocumentKeyPage>.Success(new DocumentKeyPage
        {
            Items = keys,
            ContinuationToken = continuationToken
        }));
    }

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync<T>(DocumentCountQuery query, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = DocumentQueryValidator.ValidateCount<T>("count", query, this.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<long>.Failure(validation));
        }

        return Task.FromResult(Result<long>.Success(this.ApplyCountQuery(this.Context.Query<T>(), query).LongCount()));
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = this.ValidateExactKey(documentKey);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<bool>.Failure((IResult)validation));
        }

        var exists = this.Context.Query<T>().Any(e =>
            e.DocumentKey.PartitionKey == documentKey.PartitionKey &&
            e.DocumentKey.RowKey == documentKey.RowKey);

        return Task.FromResult(Result<bool>.Success(exists));
    }

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = this.ValidateExactKey(documentKey);
        if (validation.IsFailure)
        {
            return Task.FromResult(validation);
        }

        if (entity is null)
        {
            return Task.FromResult(Result.Failure(new DocumentStoreInvalidQueryError("Document entity must not be null.")));
        }

        this.Context.AddOrUpdate(entity.Clone(), documentKey);

        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (entities is null)
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("Document entities must not be null."));
        }

        var materialized = entities.ToList();
        foreach (var (documentKey, entity) in materialized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await this.UpsertResultAsync(documentKey, entity, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result> DeleteResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = this.ValidateExactKey(documentKey);
        if (validation.IsFailure)
        {
            return Task.FromResult(validation);
        }

        this.Context.Delete<T>(documentKey);

        return Task.FromResult(Result.Success());
    }

    private Result ValidateExactKey(DocumentKey documentKey)
    {
        if (string.IsNullOrWhiteSpace(documentKey.PartitionKey))
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("PartitionKey must not be null or whitespace."));
        }

        if (string.IsNullOrWhiteSpace(documentKey.RowKey))
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("RowKey must not be null or whitespace."));
        }

        return Result.Success();
    }

    private IEnumerable<(DocumentKey DocumentKey, T Content)> ApplyQuery<T>(
        IEnumerable<(DocumentKey DocumentKey, T Content)> rows,
        DocumentQuery query)
        where T : class, new()
    {
        query ??= new DocumentQuery();

        return this.ApplyCountQuery(rows, new DocumentCountQuery
        {
            DocumentKey = query.DocumentKey,
            Filter = query.Filter,
            AllowFullScan = query.AllowFullScan
        });
    }

    private IEnumerable<(DocumentKey DocumentKey, T Content)> ApplyCountQuery<T>(
        IEnumerable<(DocumentKey DocumentKey, T Content)> rows,
        DocumentCountQuery query)
        where T : class, new()
    {
        query ??= new DocumentCountQuery();
        if (query.DocumentKey is null)
        {
            return rows;
        }

        var key = query.DocumentKey.Value;
        return query.Filter switch
        {
            DocumentKeyFilter.FullMatch => rows.Where(e =>
                e.DocumentKey.PartitionKey == key.PartitionKey &&
                e.DocumentKey.RowKey == key.RowKey),
            DocumentKeyFilter.RowKeyPrefixMatch => rows.Where(e =>
                e.DocumentKey.PartitionKey == key.PartitionKey &&
                e.DocumentKey.RowKey.StartsWith(key.RowKey ?? string.Empty, StringComparison.Ordinal)),
            DocumentKeyFilter.RowKeySuffixMatch => rows.Where(e =>
                e.DocumentKey.PartitionKey == key.PartitionKey &&
                e.DocumentKey.RowKey.EndsWith(key.RowKey ?? string.Empty, StringComparison.Ordinal)),
            _ => []
        };
    }

    private IEnumerable<(DocumentKey DocumentKey, T Content)> ApplyContinuation<T>(
        IEnumerable<(DocumentKey DocumentKey, T Content)> rows,
        string nativeToken)
    {
        if (string.IsNullOrWhiteSpace(nativeToken))
        {
            return rows;
        }

        var lastKey = JsonSerializer.Deserialize<DocumentKey>(nativeToken);
        return rows.Where(e =>
            string.Compare(e.DocumentKey.PartitionKey, lastKey.PartitionKey, StringComparison.Ordinal) > 0 ||
            (e.DocumentKey.PartitionKey == lastKey.PartitionKey &&
                string.Compare(e.DocumentKey.RowKey, lastKey.RowKey, StringComparison.Ordinal) > 0));
    }

    private string CreateContinuationToken(string queryHash, DocumentKey lastKey)
    {
        var result = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = ProviderName,
            QueryHash = queryHash,
            NativeToken = JsonSerializer.Serialize(lastKey)
        });

        return result.IsSuccess ? result.Value : null;
    }
}
