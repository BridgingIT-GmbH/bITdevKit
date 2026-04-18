// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Data;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides result-based extension methods for <see cref="IDocumentStoreClient{T}" />.
/// </summary>
/// <example>
/// <code>
/// var key = new DocumentKey("people", "42");
///
/// var upsertResult = await documents.UpsertResultAsync(key, person, ct);
/// if (upsertResult.IsFailure)
/// {
///     return upsertResult;
/// }
///
/// var findResult = await documents.FindResultAsync(key, ct);
/// if (findResult.IsSuccess)
/// {
///     foreach (var item in findResult.Value)
///     {
///         Console.WriteLine(item);
///     }
/// }
/// </code>
/// </example>
public static class DocumentStoreClientResultExtensions
{
    /// <summary>
    /// Retrieves all documents as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the matching documents.</returns>
    public static async Task<Result<IEnumerable<T>>> FindResultAsync<T>(
        this IDocumentStoreClient<T> source,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<T>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<T>>.Success(await source.FindAsync(cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<T>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Retrieves the documents for the specified key as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The document key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the matching documents.</returns>
    public static async Task<Result<IEnumerable<T>>> FindResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<T>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<T>>.Success(await source.FindAsync(documentKey, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<T>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Retrieves the documents for the specified key and filter as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The document key to query.</param>
    /// <param name="filter">The filter to apply to the key lookup.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the matching documents.</returns>
    public static async Task<Result<IEnumerable<T>>> FindResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<T>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<T>>.Success(await source.FindAsync(documentKey, filter, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<T>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Lists all document keys as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the document keys.</returns>
    public static async Task<Result<IEnumerable<DocumentKey>>> ListResultAsync<T>(
        this IDocumentStoreClient<T> source,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<DocumentKey>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<DocumentKey>>.Success(await source.ListAsync(cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<DocumentKey>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Lists the document keys for the specified key as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The document key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the document keys.</returns>
    public static async Task<Result<IEnumerable<DocumentKey>>> ListResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<DocumentKey>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<DocumentKey>>.Success(await source.ListAsync(documentKey, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<DocumentKey>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Lists the document keys for the specified key and filter as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The document key to query.</param>
    /// <param name="filter">The filter to apply to the key lookup.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the document keys.</returns>
    public static async Task<Result<IEnumerable<DocumentKey>>> ListResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<IEnumerable<DocumentKey>>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<IEnumerable<DocumentKey>>.Success(await source.ListAsync(documentKey, filter, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<DocumentKey>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Counts the documents as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the number of stored documents.</returns>
    public static async Task<Result<long>> CountResultAsync<T>(
        this IDocumentStoreClient<T> source,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<long>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<long>.Success(await source.CountAsync(cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Checks whether a document exists as a result value.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The document key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result containing the existence flag.</returns>
    public static async Task<Result<bool>> ExistsResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result<bool>.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            return Result<bool>.Success(await source.ExistsAsync(documentKey, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<bool>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Upserts a document and returns a result.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The key of the document to upsert.</param>
    /// <param name="entity">The document payload to upsert.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result indicating whether the document was upserted successfully.</returns>
    public static async Task<Result> UpsertResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            await source.UpsertAsync(documentKey, entity, cancellationToken).AnyContext();
            return Result.Success();
        }
        catch (Exception ex) when (IsConcurrencyException(ex))
        {
            return Result.Failure(
                ex.GetFullMessage(),
                new ConcurrencyError { EntityType = typeof(T).Name, EntityId = $"{documentKey.PartitionKey}/{documentKey.RowKey}" });
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Upserts multiple documents and returns a result.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="entities">The documents to upsert.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result indicating whether the documents were upserted successfully.</returns>
    public static async Task<Result> UpsertResultAsync<T>(
        this IDocumentStoreClient<T> source,
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            await source.UpsertAsync(entities, cancellationToken).AnyContext();
            return Result.Success();
        }
        catch (Exception ex) when (IsConcurrencyException(ex))
        {
            return Result.Failure(ex.GetFullMessage(), new ConcurrencyError { EntityType = typeof(T).Name });
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Deletes a document and returns a result.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="source">The source document-store client.</param>
    /// <param name="documentKey">The key of the document to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A result indicating whether the document was deleted successfully.</returns>
    public static async Task<Result> DeleteResultAsync<T>(
        this IDocumentStoreClient<T> source,
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        if (source is null)
        {
            return Result.Failure("Document store client cannot be null", new ArgumentError(nameof(source)));
        }

        try
        {
            await source.DeleteAsync(documentKey, cancellationToken).AnyContext();
            return Result.Success();
        }
        catch (Exception ex) when (IsConcurrencyException(ex))
        {
            return Result.Failure(
                ex.GetFullMessage(),
                new ConcurrencyError { EntityType = typeof(T).Name, EntityId = $"{documentKey.PartitionKey}/{documentKey.RowKey}" });
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    private static bool IsConcurrencyException(Exception ex) =>
        ex is DBConcurrencyException or TimeoutException ||
        ex.GetType().Name is "DbUpdateConcurrencyException" or "OptimisticConcurrencyException" or "ConcurrencyException";
}
