// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Shares native entity selection and mutation mechanics between LiteDB repository adapters.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed class LiteDbRepositoryStore<TEntity>
    where TEntity : class, IEntity
{
    private readonly ILiteDbRepositoryOptions options;
    private readonly LiteDbEntityIdentity identity;
    private Dictionary<IConcurrency, Guid> localVersions;

    /// <summary>Creates a store using an application context or registered scoped session.</summary>
    /// <param name="options">The repository configuration.</param>
    public LiteDbRepositoryStore(ILiteDbRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Session is null) { ArgumentNullException.ThrowIfNull(options.DbContext, nameof(options.DbContext)); }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxMaterializedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(options.MaxMaterializedRows, int.MaxValue);
        this.options = options;
        this.identity = new LiteDbEntityIdentity(typeof(TEntity).GetProperty("Id")?.PropertyType
            ?? throw new ArgumentException("The entity must expose its typed Id property."));
        this.CollectionName = options.CollectionName ?? options.DbContext.Database.GetCollection<TEntity>().Name;
        this.Query = new LiteDbActiveEntityQuery<TEntity>(new LiteDbActiveEntityProviderOptions<TEntity>
        {
            CollectionName = this.CollectionName, MaxMaterializedRows = options.MaxMaterializedRows
        });
        if (options.Session is null) { this.identity.Configure(options.DbContext.Database.Mapper); }
    }

    /// <summary>Gets the selected collection name.</summary>
    public string CollectionName { get; }

    /// <summary>Gets the shared query evaluator.</summary>
    public LiteDbActiveEntityQuery<TEntity> Query { get; }

    /// <summary>Executes and materializes a native operation under its optional scoped session.</summary>
    /// <typeparam name="T">The operation result.</typeparam>
    /// <param name="work">Synchronous native database work.</param>
    /// <param name="write">Whether the work requires an atomic write transaction.</param>
    /// <param name="cancellationToken">Cancels admission and unfinished batches.</param>
    /// <returns>The operation result after native completion.</returns>
    public async Task<T> ExecuteAsync<T>(Func<LiteDatabase, T> work, bool write, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (this.options.Session is not null)
        {
            var result = await this.options.Session.ExecuteAsync(async () => Result<T>.Success(
                await this.options.Session.NativeAsync(work, cancellationToken).ConfigureAwait(false)), write, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) { throw new LiteDbProviderException(result); }

            return result.Value;
        }

        var database = this.options.DbContext.Database;
        var ownsTransaction = write && database.BeginTrans();
        var previousVersions = this.localVersions;
        if (ownsTransaction) { this.localVersions = new(ReferenceEqualityComparer.Instance); }

        try
        {
            var result = work(database);
            cancellationToken.ThrowIfCancellationRequested();
            if (ownsTransaction && !database.Commit()) { throw new InvalidOperationException("The native transaction did not commit."); }

            return result;
        }
        catch
        {
            if (ownsTransaction)
            {
                try { database.Rollback(); }
                finally
                {
                    foreach (var entry in this.localVersions) { entry.Key.ConcurrencyVersion = entry.Value; }
                }
            }

            throw;
        }
        finally { this.localVersions = previousVersions; }
    }

    /// <summary>Normalizes an identity before query translation.</summary>
    /// <param name="id">The supplied identity.</param>
    /// <returns>The BSON identity.</returns>
    public BsonValue Identity(object id) => this.identity.ToBson(id);

    /// <summary>Reads one entity by identity with native include support.</summary>
    /// <param name="database">The owned database.</param>
    /// <param name="id">The identity.</param>
    /// <param name="options">Include settings.</param>
    /// <returns>The detached entity, or null.</returns>
    public TEntity Find(LiteDatabase database, object id, IFindOptions<TEntity> options = null) => id is null ? null
        : this.Query.Build(database, [], options).Where(BsonExpression.Create("$._id = @0", this.identity.ToBson(id))).FirstOrDefault();

    /// <summary>Atomically inserts or updates one entity and checks its concurrency version.</summary>
    /// <param name="database">The owned database.</param>
    /// <param name="entity">The entity, or null for no action.</param>
    /// <param name="insertOnly">Whether an existing identity is a conflict.</param>
    /// <param name="source">The capture source.</param>
    /// <param name="affectedEntityCount">Optional selected set size.</param>
    /// <param name="operationId">Optional shared operation identity.</param>
    /// <returns>The entity and confirmed action.</returns>
    public (TEntity entity, RepositoryActionResult action) Upsert(LiteDatabase database, TEntity entity,
        bool insertOnly = false, string source = "Repository", Guid? operationId = null, int? affectedEntityCount = null)
    {
        if (entity is null) { return (null, RepositoryActionResult.None); }

        var collection = database.GetCollection<BsonDocument>(this.CollectionName, this.identity.AutoId);
        var before = this.identity.IsDefault(entity.Id) ? null : collection.FindById(this.identity.ToBson(entity.Id));
        if (insertOnly && before is not null) { throw new LiteDbProviderException(new ConflictError("The entity already exists.")); }

        if (before is not null && this.options.EnableOptimisticConcurrency && entity is IConcurrency expected &&
            database.Mapper.ToObject<TEntity>(before) is IConcurrency stored && expected.ConcurrencyVersion != stored.ConcurrencyVersion)
        {
            throw new LiteDbProviderException(new ConcurrencyError("The stored entity has a different concurrency version."));
        }

        var originalVersion = (entity as IConcurrency)?.ConcurrencyVersion;
        try
        {
            if (entity is IConcurrency concurrency)
            {
                this.options.Session?.TrackVersion(concurrency);
                this.localVersions?.TryAdd(concurrency, concurrency.ConcurrencyVersion);
                concurrency.ConcurrencyVersion = GuidGenerator.CreateSequential();
            }

            var document = database.Mapper.ToDocument(entity);
            if (before is null)
            {
                this.identity.Prepare(entity, document);
                entity.Id = this.identity.FromBson(collection.Insert(document));
            }
            else if (!collection.Update(document)) { throw new LiteDbProviderException(new NotFoundError()); }

            this.options.Session?.CaptureMutation(database, entity, before, database.Mapper.ToDocument(entity), this.CollectionName,
                before is null ? "Insert" : "Update", source, operationId, affectedEntityCount);
            return (entity, before is null ? RepositoryActionResult.Inserted : RepositoryActionResult.Updated);
        }
        catch
        {
            if (entity is IConcurrency concurrency && originalVersion.HasValue) { concurrency.ConcurrencyVersion = originalVersion.Value; }

            throw;
        }
    }

    /// <summary>Deletes an entity atomically, preserving concurrency checks and mutation capture.</summary>
    /// <param name="database">The owned database.</param>
    /// <param name="id">The identity.</param>
    /// <param name="expected">Optional caller version to compare.</param>
    /// <param name="source">The capture source.</param>
    /// <param name="affectedEntityCount">Optional selected set size.</param>
    /// <param name="operationId">Optional shared operation identity.</param>
    /// <returns>The confirmed delete action.</returns>
    public RepositoryActionResult Delete(LiteDatabase database, object id, TEntity expected = null, string source = "Repository", Guid? operationId = null, int? affectedEntityCount = null)
    {
        if (id is null) { return RepositoryActionResult.None; }

        var collection = database.GetCollection<BsonDocument>(this.CollectionName);
        var before = collection.FindById(this.identity.ToBson(id));
        if (before is null) { return RepositoryActionResult.None; }

        var entity = database.Mapper.ToObject<TEntity>(before);
        if (this.options.EnableOptimisticConcurrency && expected is IConcurrency supplied && entity is IConcurrency stored && supplied.ConcurrencyVersion != stored.ConcurrencyVersion)
        {
            throw new LiteDbProviderException(new ConcurrencyError("The stored entity has a different concurrency version."));
        }

        if (!collection.Delete(this.identity.ToBson(id))) { return RepositoryActionResult.None; }

        this.options.Session?.CaptureMutation(database, expected ?? entity, before, null, this.CollectionName, "Delete", source, operationId, affectedEntityCount);
        return RepositoryActionResult.Deleted;
    }
}
