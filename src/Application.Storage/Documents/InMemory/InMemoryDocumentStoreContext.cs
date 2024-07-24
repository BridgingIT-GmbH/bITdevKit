// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BridgingIT.DevKit.Common;

public class InMemoryDocumentStoreContext(IEnumerable<DocumentEntity> entities)
{
    private readonly ReaderWriterLockSlim @lock = new();

    public InMemoryDocumentStoreContext()
        : this(Enumerable.Empty<DocumentEntity>())
    {
    }

    protected List<DocumentEntity> Entities { get; } = entities.SafeNull().ToList();

    public virtual IEnumerable<T> Find<T>()
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities
                .Where(e => e.Type == typeof(T).FullName)
                .Select(e => e.Content as T).Where(e => e is not null).ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public virtual IEnumerable<T> Find<T>(DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities
                .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
                .Select(e => e.Content as T).Where(e => e is not null).ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public virtual IEnumerable<T> Find<T>(DocumentKey documentKey, DocumentKeyFilter filter)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
                    .Select(e => e.Content as T).Where(e => e is not null).ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey.StartsWith(documentKey.RowKey))
                    .Select(e => e.Content as T).Where(e => e is not null).ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey.EndsWith(documentKey.RowKey))
                    .Select(e => e.Content as T).Where(e => e is not null).ToList();
            }

            return [];
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public virtual IEnumerable<DocumentKey> List<T>()
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            return this.Entities
                .Where(e => e.Type == typeof(T).FullName)
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public virtual IEnumerable<DocumentKey> List<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter)
        where T : class, new()
    {
        this.@lock.EnterReadLock();

        try
        {
            if (filter == DocumentKeyFilter.FullMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey.StartsWith(documentKey.RowKey))
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList();
            }
            else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
            {
                return this.Entities
                    .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey.EndsWith(documentKey.RowKey))
                    .Select(e => new DocumentKey(e.PartitionKey, e.RowKey)).ToList();
            }

            return [];
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public virtual void AddOrUpdate<T>(T content, DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterWriteLock();

        try
        {
            DateTimeOffset createdDate = DateTime.UtcNow;

            foreach (var documentEntity in this.Entities
               .Where(e => e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey).SafeNull())
            {
                createdDate = documentEntity.CreatedDate;
                this.Entities.Remove(documentEntity);
            }

            this.Entities.Add(
                new DocumentEntity
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

    public virtual void Delete<T>(DocumentKey documentKey)
        where T : class, new()
    {
        this.@lock.EnterWriteLock();

        try
        {
            this.Entities.RemoveAll(e =>
                e.Type == typeof(T).FullName && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }
}