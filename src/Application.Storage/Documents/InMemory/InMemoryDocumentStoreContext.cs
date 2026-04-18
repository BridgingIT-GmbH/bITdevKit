// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides the thread-safe backing store used by <see cref="InMemoryDocumentStoreProvider" />.
/// </summary>
public class InMemoryDocumentStoreContext(IEnumerable<DocumentEntity> entities)
{
    private readonly ReaderWriterLockSlim @lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDocumentStoreContext" /> class.
    /// </summary>
    public InMemoryDocumentStoreContext()
        : this([]) { }

    /// <summary>
    /// Gets the in-memory document entities tracked by the context.
    /// </summary>
    protected List<DocumentEntity> Entities { get; } = entities.SafeNull().ToList();

    /// <summary>
    /// Returns all documents of type <typeparamref name="T" />.
    /// </summary>
    public virtual IEnumerable<T> Find<T>()
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities.Where(e => e.Type == typeof(T).FullName)
                .Select(e => e.Content as T)
                .Where(e => e is not null)
                .ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns all documents of type <typeparamref name="T" /> matching the supplied exact key.
    /// </summary>
    public virtual IEnumerable<T> Find<T>(DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities
                .Where(e => e.Type == typeof(T).FullName &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey == documentKey.RowKey)
                .Select(e => e.Content as T)
                .Where(e => e is not null)
                .ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns all documents of type <typeparamref name="T" /> matching the supplied key filter.
    /// </summary>
    public virtual IEnumerable<T> Find<T>(DocumentKey documentKey, DocumentKeyFilter filter)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey == documentKey.RowKey)
                    .Select(e => e.Content as T)
                    .Where(e => e is not null)
                    .ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey.StartsWith(documentKey.RowKey))
                    .Select(e => e.Content as T)
                    .Where(e => e is not null)
                    .ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey.EndsWith(documentKey.RowKey))
                    .Select(e => e.Content as T)
                    .Where(e => e is not null)
                    .ToList();
            }

            return [];
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Lists all document keys for documents of type <typeparamref name="T" />.
    /// </summary>
    public virtual IEnumerable<DocumentKey> List<T>()
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities.Where(e => e.Type == typeof(T).FullName)
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                .ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Lists document keys of type <typeparamref name="T" /> using the supplied key filter.
    /// </summary>
    public virtual IEnumerable<DocumentKey> List<T>(DocumentKey documentKey, DocumentKeyFilter filter)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey == documentKey.RowKey)
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                    .ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey.StartsWith(documentKey.RowKey))
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                    .ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName &&
                        e.PartitionKey == documentKey.PartitionKey &&
                        e.RowKey.EndsWith(documentKey.RowKey))
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                    .ToList();
            }

            return [];
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a new document or replaces the existing document stored under the supplied key.
    /// </summary>
    public virtual void AddOrUpdate<T>(T content, DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterWriteLock();

        try
        {
            DateTimeOffset createdDate = DateTime.UtcNow;

            foreach (var documentEntity in this.Entities.Where(e =>
                             e.Type == typeof(T).FullName &&
                             e.PartitionKey == documentKey.PartitionKey &&
                             e.RowKey == documentKey.RowKey)
                         .SafeNull())
            {
                createdDate = documentEntity.CreatedDate;
                this.Entities.Remove(documentEntity);
            }

            this.Entities.Add(new DocumentEntity
            {
                Type = typeof(T).FullName,
                PartitionKey = documentKey.PartitionKey,
                RowKey = documentKey.RowKey,
                CreatedDate = createdDate,
                Content = content
            });
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Deletes the document of type <typeparamref name="T" /> stored under the supplied key.
    /// </summary>
    public virtual void Delete<T>(DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterWriteLock();

        try
        {
            this.Entities.RemoveAll(e =>
                e.Type == typeof(T).FullName &&
                e.PartitionKey == documentKey.PartitionKey &&
                e.RowKey == documentKey.RowKey);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }
}
