// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using System.Transactions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Executes exactly one provider-native entity bulk insert operation.
/// </summary>
/// <typeparam name="TEntity">The entity type to insert.</typeparam>
/// <typeparam name="TContext">The Entity Framework context that owns the entity mapping and transaction.</typeparam>
/// <example>
/// <code>
/// var inserter = serviceProvider.GetRequiredService&lt;IEntityBulkInserter&lt;Person&gt;&gt;();
/// var result = await inserter.InsertAsync(people, cancellationToken);
/// </code>
/// </example>
public sealed partial class EntityFrameworkEntityBulkInserter<TEntity, TContext>
    : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private readonly ILogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>> logger;
    private readonly TContext context;
    private readonly EntityBulkInsertMappingBuilder<TEntity> mappingBuilder;
    private readonly EntityBulkInsertOptions options;
    private readonly IReadOnlyList<IEntityBulkInsertProvider> providers;

    internal EntityFrameworkEntityBulkInserter(
        ILoggerFactory loggerFactory,
        TContext context,
        EntityBulkInsertMappingBuilder<TEntity> mappingBuilder,
        EntityBulkInsertOptions options,
        IEnumerable<IEntityBulkInsertProvider> providers)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.mappingBuilder = mappingBuilder ?? throw new ArgumentNullException(nameof(mappingBuilder));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.providers = (providers ?? []).Where(provider => provider is not null).ToArray();
        this.logger = loggerFactory?.CreateLogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<EntityFrameworkEntityBulkInserter<TEntity, TContext>>();
    }

    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = Materialize(entities);
        if (items.Count == 0)
        {
            return Result<long>.Success(0);
        }

        var providerName = this.context.Database.ProviderName ?? string.Empty;
        var providerResult = this.SelectProvider(providerName);
        if (providerResult.IsFailure)
        {
            return Failure(providerResult.Messages, providerResult.Errors);
        }

        var provider = providerResult.Value;
        if (!provider.IsSupported)
        {
            return Result<long>.Failure().WithError(new EntityBulkInsertPreconditionError(
                "provider.support",
                string.IsNullOrWhiteSpace(provider.UnsupportedReason)
                    ? $"Entity bulk insert is not supported by provider '{GetDisplayProviderName(providerName)}'."
                    : provider.UnsupportedReason));
        }

        var precondition = this.ValidatePreconditions(providerName);
        if (precondition is not null)
        {
            return Result<long>.Failure().WithError(precondition);
        }

        EntityBulkInsertBatch<TEntity> batch;
        try
        {
            this.options.Validate();
            var analysis = this.mappingBuilder.Analyze(this.context, items, this.options);
            batch = this.mappingBuilder.Build(analysis);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<long>.Failure().WithError(new EntityBulkInsertPreconditionError(
                "mapping",
                exception.Message));
        }

        return await this.ExecuteNativeAsync(provider, providerName, batch, cancellationToken).AnyContext();
    }

    private async Task<Result<long>> ExecuteNativeAsync(
        IEntityBulkInsertProvider provider,
        string providerName,
        EntityBulkInsertBatch<TEntity> batch,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = this.context.Database.CurrentTransaction is null;
        var relational = this.context.Database.IsRelational();
        var openedConnection = ownsTransaction && relational &&
            this.context.Database.GetDbConnection().State is not ConnectionState.Open;
        IDbContextTransaction transaction = null;
        var stage = "connection";

        try
        {
            if (openedConnection)
            {
                await this.context.Database.OpenConnectionAsync(cancellationToken).AnyContext();
            }

            if (ownsTransaction)
            {
                stage = "transaction.begin";
                transaction = await this.context.Database.BeginTransactionAsync(cancellationToken).AnyContext();
            }

            stage = "provider";
            TypedLogger.LogBulkInsert(
                this.logger,
                Infrastructure.EntityFramework.Constants.LogKey,
                typeof(TEntity).Name,
                batch.Entities.Count,
                providerName,
                provider.GetType().Name);
            var inserted = await provider.InsertAsync(this.context, batch, cancellationToken).AnyContext();

            if (ownsTransaction)
            {
                stage = "transaction.commit";
                await transaction.CommitAsync(cancellationToken).AnyContext();
            }

            return Result<long>.Success(inserted);
        }
        catch (OperationCanceledException)
        {
            await RollbackOwnedTransactionAsync(transaction).AnyContext();
            throw;
        }
        catch (Exception exception)
        {
            var errors = new List<IResultError>
            {
                new EntityBulkInsertProviderError(stage, providerName, exception.Message, exception),
            };
            errors.AddRange(await RollbackOwnedTransactionAsync(transaction).AnyContext());
            return Result<long>.Failure().WithErrors(errors);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().AnyContext();
            }

            if (openedConnection)
            {
                await this.context.Database.CloseConnectionAsync().AnyContext();
            }
        }
    }

    private EntityBulkInsertPreconditionError ValidatePreconditions(string providerName)
    {
        if (Transaction.Current is not null)
        {
            return new EntityBulkInsertPreconditionError(
                "transaction.ambient",
                "Native entity bulk insert does not support an ambient System.Transactions transaction. Use an explicit EF transaction instead.");
        }

        var executionStrategy = this.context.Database.CreateExecutionStrategy();
        if (this.context.Database.CurrentTransaction is null && executionStrategy.RetriesOnFailure)
        {
            return new EntityBulkInsertPreconditionError(
                "transaction.retry-strategy",
                $"Native entity bulk insert for provider '{GetDisplayProviderName(providerName)}' cannot own a transaction while the EF execution strategy retries. Run inside a caller-owned retry scope and EF transaction.");
        }

        return null;
    }

    private static IReadOnlyList<TEntity> Materialize(IEnumerable<TEntity> entities)
    {
        if (entities is IReadOnlyList<TEntity> items && items.All(entity => entity is not null))
        {
            return items;
        }

        return (entities ?? []).Where(entity => entity is not null).ToArray();
    }

    private static async Task<IReadOnlyList<IResultError>> RollbackOwnedTransactionAsync(IDbContextTransaction transaction)
    {
        if (transaction is null)
        {
            return [];
        }

        try
        {
            await transaction.RollbackAsync(CancellationToken.None).AnyContext();
            return [];
        }
        catch (Exception exception)
        {
            return [Result.Settings.ExceptionErrorFactory(exception)];
        }
    }

    private Result<IEntityBulkInsertProvider> SelectProvider(string providerName)
    {
        try
        {
            return Result<IEntityBulkInsertProvider>.Success(this.GetProvider(providerName));
        }
        catch (Exception exception)
        {
            return Result<IEntityBulkInsertProvider>.Failure().WithError(
                new EntityBulkInsertProviderError("provider-selection", providerName, exception.Message, exception));
        }
    }

    private IEntityBulkInsertProvider GetProvider(string providerName)
    {
        var matches = this.providers.Where(provider =>
            string.Equals(provider.ProviderName, providerName, StringComparison.Ordinal)).ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new NotSupportedException(
                $"Bulk insert for '{typeof(TEntity).Name}' does not have a registered provider for '{GetDisplayProviderName(providerName)}'. Registered providers: {GetRegisteredProviderNames(this.providers)}."),
            _ => throw new InvalidOperationException(
                $"Bulk insert for '{typeof(TEntity).Name}' has multiple registered providers for '{GetDisplayProviderName(providerName)}': {string.Join(", ", matches.Select(provider => provider.GetType().FullName))}."),
        };
    }

    private static Result<long> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors) =>
        Result<long>.Failure().WithMessages(messages ?? []).WithErrors(errors ?? []);

    private static string GetDisplayProviderName(string providerName) =>
        string.IsNullOrWhiteSpace(providerName) ? "<unknown>" : providerName;

    private static string GetRegisteredProviderNames(IEnumerable<IEntityBulkInsertProvider> providers)
    {
        var names = providers.Select(provider => provider.ProviderName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return names.Length == 0 ? "<none>" : string.Join(", ", names);
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
