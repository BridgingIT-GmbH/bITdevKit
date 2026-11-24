// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Cryptography;
using System.Text;

internal sealed class BlobDimensionNameResolver<TContext>(IServiceScopeFactory scopes) where TContext : DbContext
{
    private readonly Lazy<BlobDimensionNameCache> cache = new(() => new());

    private readonly System.Diagnostics.Stopwatch lifetime = System.Diagnostics.Stopwatch.StartNew();
    private long registrationAttempts;
    private long registrationConflicts;
    private long acknowledgedNames;

    public int CachedNames => this.cache.IsValueCreated ? this.cache.Value.Count : 0;

    public BlobDimensionMutationSession BeginSession() => new(this.cache.Value);

    public void PublishCommitted(BlobDimensionMutationSession session)
    {
        if (session.Published || System.Transactions.Transaction.Current is not null) return;
        session.Published = true;
        this.cache.Value.Publish(session);
        if (session.NewRegistrations > 0) Interlocked.Add(ref this.acknowledgedNames, session.NewRegistrations);
    }

    public BlobDimensionDiagnostics Capture() => new()
    {
        PositiveCacheEntries = this.CachedNames,
        RegistrationOperationsInFlight = this.cache.IsValueCreated ? 4 - this.cache.Value.RegistrationAdmission.CurrentCount : 0,
        RegistrationResolutionAttempts = Interlocked.Read(ref this.registrationAttempts),
        RegistrationConflicts = Interlocked.Read(ref this.registrationConflicts),
        AcknowledgedNameRegistrations = Interlocked.Read(ref this.acknowledgedNames),
        RegistrationsPerSecond = Interlocked.Read(ref this.acknowledgedNames) / Math.Max(1, this.lifetime.Elapsed.TotalSeconds)
    };

    public void Clear() { if (this.cache.IsValueCreated) this.cache.Value.Clear(); }

    public async Task<Result<IReadOnlyDictionary<string, long>>> ResolveForReadAsync(TContext root, string scope, IEnumerable<string> names, CancellationToken token)
    {
        await using var session = this.BeginSession();
        var result = await this.LookupAsync(root, scope, names, session, token).ConfigureAwait(false);
        if (result.IsSuccess && root.Database.CurrentTransaction is null) this.PublishCommitted(session);
        return result;
    }

    public async Task<Result<IReadOnlyDictionary<string, long>>> ResolveForWriteAsync(TContext root, string scope, IEnumerable<string> names,
        BlobDimensionMutationSession session, CancellationToken token)
    {
        var supplied = names.GroupBy(BlobDimensionValues.CanonicalizeName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var requested = supplied.Keys.ToArray();
        var result = await this.LookupAsync(root, scope, requested, session, token).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Count == requested.Length) return result;
        if (root.Database.CurrentTransaction is null) return Failed("Dimension registration requires the owner transaction.");
        await session.AdmitAsync(token).ConfigureAwait(false);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Interlocked.Increment(ref this.registrationAttempts);
            result = await this.LookupAsync(root, scope, requested, session, token).ConfigureAwait(false);
            if (result.IsFailure || result.Value.Count == requested.Length) return result;
            var missing = requested.Where(name => !result.Value.ContainsKey(name)).ToArray();
            foreach (var batch in missing.Chunk(64))
            {
                await using var serviceScope = scopes.CreateAsyncScope();
                var registration = serviceScope.ServiceProvider.GetRequiredService<TContext>();
                registration.Database.SetDbConnection(root.Database.GetDbConnection(), contextOwnsConnection: false);
                await registration.Database.UseTransactionAsync(root.Database.CurrentTransaction.GetDbTransaction(), token).ConfigureAwait(false);
                var rows = batch.Select(name => new StorageBlobDimensionName
                {
                    Scope = scope, ScopeHash = Digest(scope), Name = supplied[name], NameHash = Digest(name)
                }).ToArray();
                registration.Set<StorageBlobDimensionName>().AddRange(rows);
                try
                {
                    await registration.SaveChangesAsync(token).ConfigureAwait(false);
                    session.NewRegistrations += rows.Length;
                    foreach (var row in rows)
                    {
                        var key = Key(root, scope, BlobDimensionValues.CanonicalizeName(row.Name), session);
                        session.Resolved[key] = row.Id;
                        session.Pending[key] = row.Id;
                    }
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    Interlocked.Increment(ref this.registrationConflicts);
                    // EF rolls back its SaveChanges savepoint. This context tracked only name registrations.
                    break;
                }
            }
        }

        result = await this.LookupAsync(root, scope, requested, session, token).ConfigureAwait(false);
        return result.IsSuccess && result.Value.Count != requested.Length ? Result<IReadOnlyDictionary<string, long>>.Failure(new BlobDimensionRegistrationContentionError()) : result;
    }

    public Task<Result<IReadOnlyDictionary<string, long>>> ResolveExistingAsync(TContext root, string scope, IEnumerable<string> names,
        BlobDimensionMutationSession session, CancellationToken token) => this.LookupAsync(root, scope, names, session, token);

    private async Task<Result<IReadOnlyDictionary<string, long>>> LookupAsync(TContext root, string scope, IEnumerable<string> names,
        BlobDimensionMutationSession session, CancellationToken token)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        var requested = names.ToArray();
        if (requested.Length == 0) return Result<IReadOnlyDictionary<string, long>>.Success(result);
        if (scope is null || scope.Length > 256) return Failed("Dimension scope exceeds its persisted bounds.");
        if (root.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await root.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        var missing = new List<string>();
        foreach (var name in requested.Select(BlobDimensionValues.CanonicalizeName).Distinct(StringComparer.Ordinal))
        {
            var key = Key(root, scope, name, session);
            if (session.Resolved.TryGetValue(key, out var id) || this.cache.Value.TryGet(key, out id))
            {
                result[name] = id;
                session.Resolved[key] = id;
            }
            else missing.Add(name);
        }

        var scopeHash = Digest(scope);
        foreach (var batch in missing.Chunk(64))
        {
            var expected = new Dictionary<string, string>(StringComparer.Ordinal);
            var predicate = new PredicateBuilder<StorageBlobDimensionName>().Add(_ => false);
            foreach (var name in batch)
            {
                var hash = Digest(name);
                var hex = Convert.ToHexString(hash);
                if (!expected.TryAdd(hex, name) && expected[hex] != name)
                    return Result<IReadOnlyDictionary<string, long>>.Failure(new BlobDimensionHashCollisionError("Name"));
                predicate.Or(row => row.NameHash == hash);
            }

            var rows = await root.Set<StorageBlobDimensionName>().AsNoTracking()
                .Where(row => row.ScopeHash == scopeHash).Where(predicate.BuildExpression()).ToListAsync(token).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (!string.Equals(row.Scope, scope, StringComparison.Ordinal))
                    return Result<IReadOnlyDictionary<string, long>>.Failure(new BlobDimensionHashCollisionError("Scope"));
                var name = expected[Convert.ToHexString(row.NameHash)];
                if (!BlobDimensionValues.IsValidName(row.Name) || BlobDimensionValues.CanonicalizeName(row.Name) != name)
                    return Result<IReadOnlyDictionary<string, long>>.Failure(new BlobDimensionHashCollisionError("Name"));
                var key = Key(root, scope, name, session);
                session.Resolved[key] = row.Id;
                session.Pending[key] = row.Id;
                result[name] = row.Id;
            }
        }

        return Result<IReadOnlyDictionary<string, long>>.Success(result);
    }

    private static byte[] Digest(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

    private static BlobDimensionNameKey Key(TContext context, string scope, string name, BlobDimensionMutationSession session)
    {
        if (session.StoreIdentity is null)
        {
            // Password redaction after opening a connection must not change cache identity.
            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = context.Database.GetDbConnection().ConnectionString };
            builder.Remove("Password");
            builder.Remove("Pwd");
            builder.Remove("Access Token");
            var settings = builder.Keys.Cast<string>().OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Select(key => new[] { key.ToUpperInvariant(), Convert.ToString(builder[key], System.Globalization.CultureInfo.InvariantCulture) }).ToArray();
            var entity = context.Model.FindEntityType(typeof(StorageBlobDimensionName));
            session.StoreIdentity = HashHelper.ComputeSha256(System.Text.Json.JsonSerializer.Serialize(new object[]
            {
                context.Database.ProviderName, context.Database.GetDbConnection().DataSource, context.Database.GetDbConnection().Database,
                entity.GetSchema(), entity.GetTableName(), settings
            }));
        }

        return new(session.StoreIdentity, scope, name);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        if (exception.InnerException is Microsoft.Data.SqlClient.SqlException sql) return sql.Number is 2601 or 2627;
        var inner = exception.InnerException;
        if (inner?.GetType().GetProperty("SqlState")?.GetValue(inner) is string state) return state == "23505";
        return inner?.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(inner) is int code && code is 1555 or 2067;
    }

    private static Result<IReadOnlyDictionary<string, long>> Failed(string message) =>
        Result<IReadOnlyDictionary<string, long>>.Failure(new BlobStoreProviderError(message));
}
