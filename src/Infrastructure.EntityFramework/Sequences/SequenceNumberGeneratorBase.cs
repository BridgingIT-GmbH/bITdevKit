// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Base implementation for sequence number generators with thread-safe operations.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public abstract partial class SequenceNumberGeneratorBase<TContext> : ISequenceNumberGenerator
    where TContext : DbContext
{
    private readonly ILoggerFactory loggerFactory;
    /// <summary>
    /// Stores the logger.
    /// </summary>
    protected readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    /// <summary>
    /// Stores the context type name.
    /// </summary>
    protected readonly string contextTypeName;
    /// <summary>
    /// Stores the options.
    /// </summary>
    protected readonly SequenceNumberGeneratorOptions options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> sequenceLocks;

    /// <summary>
    /// Initializes a new instance of the <c>SequenceNumberGeneratorBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="serviceProvider">The service provider used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    protected SequenceNumberGeneratorBase(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        SequenceNumberGeneratorOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory?.CreateLogger(this.GetType()) ?? NullLoggerFactory.Instance.CreateLogger(this.GetType());
        this.serviceProvider = serviceProvider;
        this.contextTypeName = typeof(TContext).Name;
        this.options = options ?? new SequenceNumberGeneratorOptions();
        this.sequenceLocks = [];
    }

    /// <summary>
    /// Gets next.
    /// </summary>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<long>> GetNextAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(sequenceName, schema);
        var semaphore = this.sequenceLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        var lockTimeout = this.GetLockTimeout(sequenceName);

        TypedLogger.LogSequenceGeneration(this.logger, Constants.LogKey, sequenceName, schema ?? "default", this.contextTypeName);

        if (!await semaphore.WaitAsync(lockTimeout, cancellationToken))
        {
            TypedLogger.LogSequenceLockTimeout(this.logger, Constants.LogKey, sequenceName, lockTimeout.TotalSeconds);

            return Result<long>.Failure()
                .WithError(new SequenceLockTimeoutError(sequenceName, lockTimeout));
        }

        try
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            var result = await this.GetNextInternalAsync(context, sequenceName, schema, cancellationToken);
            if (result.IsSuccess)
            {
                TypedLogger.LogSequenceGenerated(this.logger, Constants.LogKey, result.Value, sequenceName, schema ?? "default", this.contextTypeName, context.ContextId.ToString());
            }
            else
            {
                TypedLogger.LogSequenceGenerationFailed(this.logger, Constants.LogKey, sequenceName, schema ?? "default", this.contextTypeName, context.ContextId.ToString(), result.ToString());
            }

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Gets next multiple.
    /// </summary>
    /// <param name="sequenceNames">The sequence names used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<Dictionary<string, long>>> GetNextMultipleAsync(
        IEnumerable<string> sequenceNames,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var names = sequenceNames.ToList();
        if (names.Count == 0)
        {
            return Result<Dictionary<string, long>>.Success([]);
        }

        TypedLogger.LogMultipleSequenceGeneration(this.logger, Constants.LogKey, names.Count, schema ?? "default", this.contextTypeName);

        var results = new Dictionary<string, long>();
        var errors = new List<IResultError>();

        foreach (var name in names)
        {
            var result = await this.GetNextAsync(name, schema, cancellationToken);
            if (result.IsSuccess)
            {
                results[name] = result.Value;
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count != 0)
        {
            return Result<Dictionary<string, long>>.Failure()
                .WithErrors(errors);
        }

        return Result<Dictionary<string, long>>.Success(results);
    }

    /// <summary>
    /// Gets next for entity.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<Result<long>> GetNextForEntityAsync<TEntity>(
        string schema = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entityName = typeof(TEntity).Name;
        var sequenceName = $"{entityName}Sequence";

        TypedLogger.LogEntitySequenceGeneration(this.logger, Constants.LogKey, entityName, sequenceName);

        return this.GetNextAsync(sequenceName, schema, cancellationToken);
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<bool>> ExistsAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        return await this.ExistsInternalAsync(
            context,
            sequenceName,
            schema,
            cancellationToken);
    }

    /// <summary>
    /// Gets sequence info.
    /// </summary>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<SequenceInfo>> GetSequenceInfoAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        return await this.GetSequenceInfoInternalAsync(
            context,
            sequenceName,
            schema,
            cancellationToken);
    }

    /// <summary>
    /// Gets current value.
    /// </summary>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<long>> GetCurrentValueAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        return await this.GetCurrentValueInternalAsync(
            context,
            sequenceName,
            schema,
            cancellationToken);
    }

    /// <summary>
    /// Executes the reset sequence operation.
    /// </summary>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="startValue">The start value used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> ResetSequenceAsync(
        string sequenceName,
        long startValue,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(sequenceName, schema);
        var semaphore = this.sequenceLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        var lockTimeout = this.GetLockTimeout(sequenceName);

        TypedLogger.LogSequenceReset(this.logger, Constants.LogKey, sequenceName, startValue, this.contextTypeName);

        if (!await semaphore.WaitAsync(lockTimeout, cancellationToken))
        {
            return Result.Failure()
                .WithError(new SequenceLockTimeoutError(sequenceName, lockTimeout));
        }

        try
        {
            using var scope = this.serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            var result = await this.ResetSequenceInternalAsync(
                context,
                sequenceName,
                startValue,
                schema,
                cancellationToken);

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Gets next internal.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result<long>> GetNextInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the exists internal operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result<bool>> ExistsInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets sequence info internal.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result<SequenceInfo>> GetSequenceInfoInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets current value internal.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result<long>> GetCurrentValueInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the reset sequence internal operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="sequenceName">The sequence name used by the operation.</param>
    /// <param name="startValue">The start value used by the operation.</param>
    /// <param name="schema">The schema used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<Result> ResetSequenceInternalAsync(
        TContext context,
        string sequenceName,
        long startValue,
        string schema,
        CancellationToken cancellationToken);

    private static string GetLockKey(string sequenceName, string schema)
    {
        return string.IsNullOrWhiteSpace(schema)
            ? sequenceName
            : $"{schema}.{sequenceName}";
    }

    private TimeSpan GetLockTimeout(string sequenceName)
    {
        if (this.options.SequenceOverrides.TryGetValue(sequenceName, out var seqOptions)
            && seqOptions.LockTimeout.HasValue)
        {
            return seqOptions.LockTimeout.Value;
        }

        return this.options.LockTimeout;
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the sequence generation operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        /// <param name="schema">The schema used by the operation.</param>
        /// <param name="dbContextType">The db context type used by the operation.</param>
        [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] sequence number generate: start (sequence={SequenceName}, schema={Schema}, context={DbContextType})")]
        public static partial void LogSequenceGeneration(ILogger logger, string logKey, string sequenceName, string schema, string dbContextType);

        /// <summary>
        /// Writes a log entry for the sequence generated operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="value">The value used by the operation.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        /// <param name="schema">The schema used by the operation.</param>
        /// <param name="dbContextType">The db context type used by the operation.</param>
        /// <param name="dbContextId">The db context id used by the operation.</param>
        [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] sequence number generate: generated value {Value} from {SequenceName} (schema={Schema}, context={DbContextType}/{DbContextId})")]
        public static partial void LogSequenceGenerated(ILogger logger, string logKey, long value, string sequenceName, string schema, string dbContextType, string dbContextId);

        /// <summary>
        /// Writes a log entry for the sequence generation failed operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        /// <param name="schema">The schema used by the operation.</param>
        /// <param name="dbContextType">The db context type used by the operation.</param>
        /// <param name="dbContextId">The db context id used by the operation.</param>
        /// <param name="message">The message associated with the operation.</param>
        [LoggerMessage(2, LogLevel.Error, "[{LogKey}] sequence number generate: failed (sequence={SequenceName}, schema={Schema}, context={DbContextType}/{DbContextId}) {Message}")]
        public static partial void LogSequenceGenerationFailed(ILogger logger, string logKey, string sequenceName, string schema, string dbContextType, string dbContextId, string message);

        /// <summary>
        /// Writes a log entry for the sequence lock timeout operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        /// <param name="timeout">The timeout used by the operation.</param>
        [LoggerMessage(3, LogLevel.Warning, "[{LogKey}] sequence number generate: failed to acquire lock for sequence {SequenceName} within {Timeout} seconds")]
        public static partial void LogSequenceLockTimeout(ILogger logger, string logKey, string sequenceName, double timeout);

        /// <summary>
        /// Writes a log entry for the multiple sequence generation operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="count">The number of values to process.</param>
        /// <param name="schema">The schema used by the operation.</param>
        /// <param name="dbContextType">The db context type used by the operation.</param>
        [LoggerMessage(4, LogLevel.Debug, "[{LogKey}] sequence number generate: start multiple (count={Count}, schema={Schema}, context={DbContextType})")]
        public static partial void LogMultipleSequenceGeneration(ILogger logger, string logKey, int count, string schema, string dbContextType);

        /// <summary>
        /// Writes a log entry for the entity sequence generation operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityName">The entity name used by the operation.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        [LoggerMessage(6, LogLevel.Debug, "[{LogKey}] sequence number generate: start for entity (entity={EntityName}, sequence={SequenceName})")]
        public static partial void LogEntitySequenceGeneration(ILogger logger, string logKey, string entityName, string sequenceName);

        /// <summary>
        /// Writes a log entry for the sequence reset operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="sequenceName">The sequence name used by the operation.</param>
        /// <param name="startValue">The start value used by the operation.</param>
        /// <param name="dbContextType">The db context type used by the operation.</param>
        [LoggerMessage(7, LogLevel.Information, "[{LogKey}] sequence number generate: reset (sequence={SequenceName}, startValue={StartValue}, context={DbContextType})")]
        public static partial void LogSequenceReset(ILogger logger, string logKey, string sequenceName, long startValue, string dbContextType);
    }
}
