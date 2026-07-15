// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Dispatches prepared entity bulk inserts to the strategy registered for the active Entity Framework provider.
/// </summary>
/// <typeparam name="TEntity">The entity type to insert.</typeparam>
/// <typeparam name="TContext">The Entity Framework context that owns the entity mapping and database connection.</typeparam>
/// <example>
/// <code>
/// var inserter = new EntityFrameworkEntityBulkInserter&lt;Person, AppDbContext&gt;(
///     loggerFactory,
///     dbContext,
///     new EntityBulkInsertMappingBuilder&lt;Person&gt;(),
///     options,
///     providers);
/// var result = await inserter.InsertAsync(people);
/// </code>
/// </example>
public partial class EntityFrameworkEntityBulkInserter<TEntity, TContext> : IEntityBulkInserter<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    private readonly ILogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>> logger;
    private readonly TContext context;
    private readonly EntityBulkInsertMappingBuilder<TEntity> mappingBuilder;
    private readonly EntityBulkInsertOptions options;
    private readonly IReadOnlyList<IEntityBulkInsertProvider> providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkEntityBulkInserter{TEntity, TContext}"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the inserter logger.</param>
    /// <param name="context">The active Entity Framework context.</param>
    /// <param name="mappingBuilder">The provider-neutral EF metadata and value mapping builder.</param>
    /// <param name="options">The provider-neutral bulk insert options.</param>
    /// <param name="providers">The native bulk insert strategies registered by provider packages.</param>
    /// <example>
    /// <code>
    /// var inserter = new EntityFrameworkEntityBulkInserter&lt;Person, AppDbContext&gt;(
    ///     loggerFactory,
    ///     dbContext,
    ///     mappingBuilder,
    ///     options,
    ///     providers);
    /// </code>
    /// </example>
    public EntityFrameworkEntityBulkInserter(
        ILoggerFactory loggerFactory,
        TContext context,
        EntityBulkInsertMappingBuilder<TEntity> mappingBuilder,
        EntityBulkInsertOptions options,
        IEnumerable<IEntityBulkInsertProvider> providers)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(mappingBuilder, nameof(mappingBuilder));
        EnsureArg.IsNotNull(options, nameof(options));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>>();
        this.context = context;
        this.mappingBuilder = mappingBuilder;
        this.options = options;
        this.providers = providers.SafeNull()
            .Where(provider => provider is not null)
            .ToList();
    }

    /// <inheritdoc />
    public virtual async Task<Result<long>> InsertAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = entities.SafeNull()
                .Where(entity => entity is not null)
                .ToList();
            if (items.Count == 0)
            {
                return Result<long>.Success(0);
            }

            this.options.Validate();

            var providerName = this.context.Database.ProviderName ?? string.Empty;
            var provider = this.GetProvider(providerName);
            var batch = this.mappingBuilder.Build(this.context, items, this.options);

            TypedLogger.LogBulkInsert(
                this.logger,
                Infrastructure.EntityFramework.Constants.LogKey,
                typeof(TEntity).Name,
                items.Count,
                providerName,
                provider.GetType().Name);
            var inserted = await provider.InsertAsync(this.context, batch, cancellationToken).AnyContext();

            return Result<long>.Success(inserted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<long>.Failure()
                .WithError(new ExceptionError(ex));
        }
    }

    private IEntityBulkInsertProvider GetProvider(string providerName)
    {
        var matches = this.providers
            .Where(provider => string.Equals(provider.ProviderName, providerName, StringComparison.Ordinal))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' does not have a registered provider for '{GetDisplayProviderName(providerName)}'. " +
                $"Registered providers: {GetRegisteredProviderNames(this.providers)}."),
            _ => throw new InvalidOperationException(
                $"Bulk insert for '{typeof(TEntity).Name}' has multiple registered providers for '{GetDisplayProviderName(providerName)}': " +
                $"{string.Join(", ", matches.Select(provider => provider.GetType().FullName))}.")
        };
    }

    private static string GetDisplayProviderName(string providerName)
    {
        return string.IsNullOrWhiteSpace(providerName) ? "<unknown>" : providerName;
    }

    private static string GetRegisteredProviderNames(IEnumerable<IEntityBulkInsertProvider> providers)
    {
        var names = providers
            .Select(provider => provider.ProviderName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return names.Count == 0 ? "<none>" : string.Join(", ", names);
    }

    private static partial class TypedLogger
    {
        [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] bulk inserting {EntityCount} {EntityType} entities with {EntityFrameworkProvider} using {BulkInsertProvider}")]
        public static partial void LogBulkInsert(
            ILogger logger,
            string logKey,
            string entityType,
            int entityCount,
            string entityFrameworkProvider,
            string bulkInsertProvider);
    }
}
