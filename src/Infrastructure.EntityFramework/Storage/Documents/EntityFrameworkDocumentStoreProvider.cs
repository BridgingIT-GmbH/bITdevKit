// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides an Entity Framework backed <see cref="IDocumentStoreProvider" /> with hashed lookups and
/// lease-based mutation coordination.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IDocumentStoreContext" />.</typeparam>
/// <example>
/// <code>
/// services.AddDbContext&lt;AppDbContext&gt;(options => options.UseSqlServer(connectionString));
///
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;(
///     configure: options =>
///     {
///         options.LeaseDuration = TimeSpan.FromSeconds(15);
///         options.RetryCount = 5;
///         options.RetryDelay = TimeSpan.FromMilliseconds(100);
///     });
/// </code>
/// </example>
public class EntityFrameworkDocumentStoreProvider<TContext> : IDocumentStoreProvider
    where TContext : DbContext, IDocumentStoreContext
{
    private const int MaximumKeyLength = StorageDocument.MaximumKeyLength;
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly ISerializer serializer;
    private readonly EntityFrameworkDocumentStoreProviderOptions options;
    private readonly ILogger logger;
    private readonly string leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkDocumentStoreProvider{TContext}" /> class for
    /// DI-managed usage with fresh scoped contexts per operation.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to create operation scopes.</param>
    /// <param name="loggerFactory">The logger factory used to initialize runtime options.</param>
    /// <param name="serializer">The serializer used for document payloads.</param>
    /// <param name="options">Optional runtime options for lease and retry behavior.</param>
    public EntityFrameworkDocumentStoreProvider(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory = null,
        ISerializer serializer = null,
        EntityFrameworkDocumentStoreProviderOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.serviceProvider = serviceProvider;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new EntityFrameworkDocumentStoreProviderOptions();
        this.options.LoggerFactory ??= loggerFactory ?? serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        this.logger = this.options.CreateLogger<EntityFrameworkDocumentStoreProvider<TContext>>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkDocumentStoreProvider{TContext}" /> class for
    /// manually scoped usage with a caller-owned <typeparamref name="TContext" />.
    /// </summary>
    /// <param name="context">The caller-owned database context.</param>
    /// <param name="serializer">The serializer used for document payloads.</param>
    /// <param name="options">Optional runtime options for lease and retry behavior.</param>
    public EntityFrameworkDocumentStoreProvider(
        TContext context,
        ISerializer serializer = null,
        EntityFrameworkDocumentStoreProviderOptions options = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new EntityFrameworkDocumentStoreProviderOptions();
        this.options.LoggerFactory ??= NullLoggerFactory.Instance;
        this.logger = this.options.CreateLogger<EntityFrameworkDocumentStoreProvider<TContext>>();
    }

    /// <summary>
    /// Retrieves all entities of type <typeparamref name="T" />.
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var identity = this.CreateTypeIdentity<T>();

        using var lease = this.CreateContextLease();
        var contents = await this.QueryByType(lease.Context, identity)
            .AsNoTracking()
            .OrderBy(e => e.PartitionKey)
            .ThenBy(e => e.RowKey)
            .Select(e => e.Content)
            .ToListAsync(cancellationToken);

        return contents.ConvertAll(content => this.serializer.Deserialize<T>(content));
    }

    /// <summary>
    /// Retrieves entities of type <typeparamref name="T" /> for an exact key.
    /// </summary>
    public Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
        => this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);

    /// <summary>
    /// Retrieves entities of type <typeparamref name="T" /> filtered by key semantics.
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var validatedKey = this.ValidateDocumentKey(documentKey, requireRowKey: true);
        var identity = this.CreateTypeIdentity<T>();
        var key = this.CreateKeyIdentity(validatedKey);

        using var lease = this.CreateContextLease();
        var contents = await this.ApplyDocumentKeyFilter(this.QueryByTypeAndPartition(lease.Context, identity, key), key, filter)
            .AsNoTracking()
            .OrderBy(e => e.RowKey)
            .Select(e => e.Content)
            .ToListAsync(cancellationToken);

        return contents.ConvertAll(content => this.serializer.Deserialize<T>(content));
    }

    /// <summary>
    /// Lists all document keys for type <typeparamref name="T" />.
    /// </summary>
    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var identity = this.CreateTypeIdentity<T>();

        using var lease = this.CreateContextLease();
        return await this.QueryByType(lease.Context, identity)
            .AsNoTracking()
            .OrderBy(e => e.PartitionKey)
            .ThenBy(e => e.RowKey)
            .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lists document keys for an exact key.
    /// </summary>
    public Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
        => this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);

    /// <summary>
    /// Lists document keys filtered by key semantics.
    /// </summary>
    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var validatedKey = this.ValidateDocumentKey(documentKey, requireRowKey: filter == DocumentKeyFilter.FullMatch);
        var identity = this.CreateTypeIdentity<T>();
        var key = this.CreateKeyIdentity(validatedKey);

        using var lease = this.CreateContextLease();
        return await this.ApplyDocumentKeyFilter(this.QueryByTypeAndPartition(lease.Context, identity, key), key, filter)
            .AsNoTracking()
            .OrderBy(e => e.RowKey)
            .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts the number of entities of type <typeparamref name="T" /> in the store.
    /// </summary>
    public async Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var identity = this.CreateTypeIdentity<T>();

        using var lease = this.CreateContextLease();
        return await this.QueryByType(lease.Context, identity)
            .AsNoTracking()
            .LongCountAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether an entity of type <typeparamref name="T" /> exists for the supplied exact key.
    /// </summary>
    public async Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var payload = this.CreatePayload<T>(this.ValidateDocumentKey(documentKey, requireRowKey: true), entity: null);

        using var lease = this.CreateContextLease();
        return await this.QueryExactDocument(lease.Context, payload)
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts or updates an entity of type <typeparamref name="T" /> for the supplied key.
    /// </summary>
    public async Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        await this.UpsertCoreAsync(this.CreatePayload(this.ValidateDocumentKey(documentKey, requireRowKey: true), entity), cancellationToken);
    }

    /// <summary>
    /// Inserts or updates multiple entities of type <typeparamref name="T" />.
    /// </summary>
    public async Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entities, nameof(entities));

        foreach (var (documentKey, entity) in entities.SafeNull())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.UpsertAsync(documentKey, entity, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes the entity of type <typeparamref name="T" /> for the supplied exact key.
    /// </summary>
    public async Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        await this.DeleteCoreAsync(this.CreatePayload<T>(this.ValidateDocumentKey(documentKey, requireRowKey: true), entity: null), cancellationToken);
    }

    private async Task UpsertCoreAsync(DocumentPayload payload, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 0; attempt <= this.options.RetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var lease = this.CreateContextLease();
                var dbContext = lease.Context;
                var document = await this.QueryExactDocument(dbContext, payload).SingleOrDefaultAsync(cancellationToken);

                if (document is null)
                {
                    dbContext.StorageDocuments.Add(new StorageDocument
                    {
                        Id = Guid.NewGuid(),
                        Type = payload.Type,
                        TypeHash = payload.TypeHash,
                        PartitionKey = payload.PartitionKey,
                        PartitionKeyHash = payload.PartitionKeyHash,
                        RowKey = payload.RowKey,
                        RowKeyHash = payload.RowKeyHash,
                        Content = payload.Content,
                        ContentHash = payload.ContentHash,
                        CreatedDate = DateTimeOffset.UtcNow
                    });

                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                if (!await this.TryAcquireLeaseAsync(dbContext, document, cancellationToken))
                {
                    if (attempt == this.options.RetryCount)
                    {
                        throw new TimeoutException(
                            $"Timed out acquiring a document mutation lease for '{payload.PartitionKey}/{payload.RowKey}'.");
                    }

                    await this.DelayRetryAsync(attempt, cancellationToken);
                    continue;
                }

                var contentChanged = !string.Equals(document.ContentHash, payload.ContentHash, StringComparison.Ordinal) ||
                    !string.Equals(document.Content, payload.Content, StringComparison.Ordinal);

                document.Content = payload.Content;
                document.ContentHash = payload.ContentHash;
                document.LockedBy = null;
                document.LockedUntil = null;

                if (contentChanged)
                {
                    document.UpdatedDate = DateTimeOffset.UtcNow;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (this.ShouldRetryMutationException(exception))
            {
                lastException = exception;
                this.logger.LogDebug(exception,
                    "Retrying document upsert for {Type}/{PartitionKey}/{RowKey} after transient contention (attempt {Attempt}/{RetryCount})",
                    payload.Type,
                    payload.PartitionKey,
                    payload.RowKey,
                    attempt + 1,
                    this.options.RetryCount + 1);

                if (attempt == this.options.RetryCount)
                {
                    throw;
                }

                await this.DelayRetryAsync(attempt, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Document upsert failed without a captured exception.");
    }

    private async Task DeleteCoreAsync(DocumentPayload payload, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 0; attempt <= this.options.RetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var lease = this.CreateContextLease();
                var dbContext = lease.Context;
                var document = await this.QueryExactDocument(dbContext, payload).SingleOrDefaultAsync(cancellationToken);

                if (document is null)
                {
                    return;
                }

                if (!await this.TryAcquireLeaseAsync(dbContext, document, cancellationToken))
                {
                    if (attempt == this.options.RetryCount)
                    {
                        throw new TimeoutException(
                            $"Timed out acquiring a document delete lease for '{payload.PartitionKey}/{payload.RowKey}'.");
                    }

                    await this.DelayRetryAsync(attempt, cancellationToken);
                    continue;
                }

                dbContext.StorageDocuments.Remove(document);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (this.ShouldRetryMutationException(exception))
            {
                lastException = exception;
                this.logger.LogDebug(exception,
                    "Retrying document delete for {Type}/{PartitionKey}/{RowKey} after transient contention (attempt {Attempt}/{RetryCount})",
                    payload.Type,
                    payload.PartitionKey,
                    payload.RowKey,
                    attempt + 1,
                    this.options.RetryCount + 1);

                if (attempt == this.options.RetryCount)
                {
                    throw;
                }

                await this.DelayRetryAsync(attempt, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Document delete failed without a captured exception.");
    }

    private async Task<bool> TryAcquireLeaseAsync(TContext dbContext, StorageDocument document, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseUntil = now.Add(this.options.LeaseDuration);

        if (this.SupportsExecuteUpdate(dbContext))
        {
            var currentVersion = document.ConcurrencyVersion;
            var affected = await dbContext.StorageDocuments
                .Where(e => e.Id == document.Id &&
                    e.ConcurrencyVersion == currentVersion &&
                    (e.LockedUntil == null || e.LockedUntil < now || e.LockedBy == this.leaseOwner))
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.LockedBy, _ => this.leaseOwner)
                        .SetProperty(e => e.LockedUntil, _ => leaseUntil)
                        .SetProperty(e => e.ConcurrencyVersion, _ => Guid.NewGuid()),
                    cancellationToken);

            if (affected == 0)
            {
                return false;
            }

            await dbContext.Entry(document).ReloadAsync(cancellationToken);
            return true;
        }

        if (document.LockedUntil is not null &&
            document.LockedUntil >= now &&
            !string.Equals(document.LockedBy, this.leaseOwner, StringComparison.Ordinal))
        {
            return false;
        }

        document.LockedBy = this.leaseOwner;
        document.LockedUntil = leaseUntil;
        document.ConcurrencyVersion = Guid.NewGuid();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private IQueryable<StorageDocument> QueryByType(TContext dbContext, TypeIdentity identity) =>
        dbContext.StorageDocuments.Where(e => e.TypeHash == identity.TypeHash && e.Type == identity.Type);

    private IQueryable<StorageDocument> QueryByTypeAndPartition(TContext dbContext, TypeIdentity identity, KeyIdentity key) =>
        this.QueryByType(dbContext, identity)
            .Where(e => e.PartitionKeyHash == key.PartitionKeyHash && e.PartitionKey == key.PartitionKey);

    private IQueryable<StorageDocument> QueryExactDocument(TContext dbContext, DocumentPayload payload) =>
        this.QueryByTypeAndPartition(dbContext, new TypeIdentity(payload.Type, payload.TypeHash), new KeyIdentity(payload.PartitionKey, payload.PartitionKeyHash, payload.RowKey, payload.RowKeyHash))
            .Where(e => e.RowKeyHash == payload.RowKeyHash && e.RowKey == payload.RowKey);

    private IQueryable<StorageDocument> ApplyDocumentKeyFilter(
        IQueryable<StorageDocument> query,
        KeyIdentity key,
        DocumentKeyFilter filter)
    {
        return filter switch
        {
            DocumentKeyFilter.FullMatch => query.Where(e => e.RowKeyHash == key.RowKeyHash && e.RowKey == key.RowKey),
            DocumentKeyFilter.RowKeyPrefixMatch => query.Where(e => e.RowKey.StartsWith(key.RowKey)),
            DocumentKeyFilter.RowKeySuffixMatch => query.Where(e => e.RowKey.EndsWith(key.RowKey)),
            _ => query.Where(_ => false)
        };
    }

    private TypeIdentity CreateTypeIdentity<T>() => this.CreateTypeIdentity(this.GetTypeName<T>());

    private TypeIdentity CreateTypeIdentity(string type)
    {
        var validatedType = this.ValidateRawKey(type, "document type");
        return new(validatedType, ComputeLookupHash(validatedType));
    }

    private KeyIdentity CreateKeyIdentity(DocumentKey documentKey) => new(
        documentKey.PartitionKey,
        ComputeLookupHash(documentKey.PartitionKey),
        documentKey.RowKey,
        documentKey.RowKey is null ? null : ComputeLookupHash(documentKey.RowKey));

    private DocumentPayload CreatePayload<T>(DocumentKey documentKey, T entity)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();
        var content = entity is null ? null : this.serializer.SerializeToString(entity);

        return new DocumentPayload(
            type,
            ComputeLookupHash(type),
            documentKey.PartitionKey,
            ComputeLookupHash(documentKey.PartitionKey),
            documentKey.RowKey,
            ComputeLookupHash(documentKey.RowKey),
            content,
            entity is null ? null : HashHelper.Compute(content));
    }

    private static string ComputeLookupHash(string input) => HashHelper.ComputeSha256(input);

    private string GetTypeName<T>() => this.ValidateRawKey(typeof(T).FullName.ToLowerInvariant(), "document type");

    private DocumentKey ValidateDocumentKey(DocumentKey documentKey, bool requireRowKey)
    {
        var partitionKey = this.ValidateRawKey(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        var rowKey = requireRowKey
            ? this.ValidateRawKey(documentKey.RowKey, nameof(documentKey.RowKey))
            : documentKey.RowKey.IsNullOrEmpty()
                ? documentKey.RowKey
                : this.ValidateRawKey(documentKey.RowKey, nameof(documentKey.RowKey));

        return new DocumentKey(partitionKey, rowKey);
    }

    private string ValidateRawKey(string value, string paramName)
    {
        EnsureArg.IsNotNullOrEmpty(value, paramName);
        EnsureArg.IsTrue(
            value.Length <= MaximumKeyLength,
            paramName,
            options => options.WithMessage($"The {paramName} cannot exceed {MaximumKeyLength} characters for the Entity Framework document store provider."));

        return value;
    }

    private bool ShouldRetryMutationException(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException or TimeoutException)
        {
            return true;
        }

        return this.IsUniqueConstraintViolation(exception) ||
            this.IsTransientLockContentionException(exception);
    }

    private bool IsUniqueConstraintViolation(Exception exception)
    {
        var providerName = this.GetProviderName(exception);
        var current = exception;

        while (current is not null)
        {
            if ((providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("SqlException", StringComparison.OrdinalIgnoreCase)) &&
                this.TryGetExceptionIntProperty(current, "Number", out var sqlServerNumber) &&
                sqlServerNumber is 2601 or 2627)
            {
                return true;
            }

            if ((providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("PostgresException", StringComparison.OrdinalIgnoreCase)) &&
                this.TryGetExceptionStringProperty(current, "SqlState", out var sqlState) &&
                sqlState == "23505")
            {
                return true;
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                if (this.TryGetExceptionIntProperty(current, "SqliteErrorCode", out var sqliteErrorCode) &&
                    sqliteErrorCode == 19)
                {
                    return true;
                }

                if (current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            current = current.InnerException;
        }

        return false;
    }

    private bool IsTransientLockContentionException(Exception exception)
    {
        var providerName = this.GetProviderName(exception);
        var current = exception;

        while (current is not null)
        {
            if (current is TimeoutException)
            {
                return true;
            }

            if ((providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("SqlException", StringComparison.OrdinalIgnoreCase)) &&
                this.TryGetExceptionIntProperty(current, "Number", out var sqlServerNumber) &&
                sqlServerNumber is 1205 or 1222 or 3960 or 41302 or 41305 or 41325 or 41839)
            {
                return true;
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                if (this.TryGetExceptionIntProperty(current, "SqliteErrorCode", out var sqliteErrorCode) &&
                    sqliteErrorCode is 5 or 6)
                {
                    return true;
                }

                if (current.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("database is busy", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if ((providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("PostgresException", StringComparison.OrdinalIgnoreCase)) &&
                this.TryGetExceptionStringProperty(current, "SqlState", out var sqlState) &&
                sqlState is "40001" or "40P01" or "55P03")
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private string GetProviderName(Exception exception)
    {
        if (this.context is not null)
        {
            return this.context.Database.ProviderName ?? string.Empty;
        }

        var current = exception;
        while (current is not null)
        {
            var fullName = current.GetType().FullName;
            if (fullName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true ||
                fullName?.Contains("SqlException", StringComparison.OrdinalIgnoreCase) == true ||
                fullName?.Contains("PostgresException", StringComparison.OrdinalIgnoreCase) == true)
            {
                return fullName;
            }

            current = current.InnerException;
        }

        return string.Empty;
    }

    private bool SupportsExecuteUpdate(TContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName;

        return providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true ||
            providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task DelayRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = this.CalculateRetryDelay(attempt + 1);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private TimeSpan CalculateRetryDelay(int attempt)
    {
        if (this.options.RetryDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var multiplier = 1L << Math.Min(Math.Max(0, attempt - 1), 6);
        var ticks = this.options.RetryDelay.Ticks;
        if (ticks > long.MaxValue / multiplier)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(ticks * multiplier);
    }

    private bool TryGetExceptionIntProperty(Exception exception, string propertyName, out int value)
    {
        var property = exception.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(int) &&
            property.GetValue(exception) is int intValue)
        {
            value = intValue;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetExceptionStringProperty(Exception exception, string propertyName, out string value)
    {
        var property = exception.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(string) &&
            property.GetValue(exception) is string stringValue)
        {
            value = stringValue;
            return true;
        }

        value = null;
        return false;
    }

    private ContextLease CreateContextLease()
    {
        if (this.serviceProvider is null)
        {
            this.context.ChangeTracker.Clear();
            return new ContextLease(this.context);
        }

        var scope = this.serviceProvider.CreateScope();
        return new ContextLease(scope, scope.ServiceProvider.GetRequiredService<TContext>());
    }

    private readonly record struct TypeIdentity(string Type, string TypeHash);

    private readonly record struct KeyIdentity(string PartitionKey, string PartitionKeyHash, string RowKey, string RowKeyHash);

    private readonly record struct DocumentPayload(
        string Type,
        string TypeHash,
        string PartitionKey,
        string PartitionKeyHash,
        string RowKey,
        string RowKeyHash,
        string Content,
        string ContentHash);

    private sealed class ContextLease : IDisposable
    {
        private readonly IServiceScope scope;
        private readonly TContext externalContext;

        public ContextLease(TContext context)
        {
            this.Context = context;
            this.externalContext = context;
        }

        public ContextLease(IServiceScope scope, TContext context)
        {
            this.scope = scope;
            this.Context = context;
        }

        public TContext Context { get; }

        public void Dispose()
        {
            if (this.scope is not null)
            {
                this.scope.Dispose();
            }
            else
            {
                this.externalContext?.ChangeTracker.Clear();
            }
        }
    }
}
