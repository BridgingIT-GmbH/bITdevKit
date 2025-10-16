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
using System.Diagnostics;
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
    protected readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;
    protected readonly string contextTypeName;
    protected readonly SequenceNumberGeneratorOptions options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> sequenceLocks;

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

    public async Task<Result<long>> GetNextAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(sequenceName, schema);
        var semaphore = this.sequenceLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        var lockTimeout = this.GetLockTimeout(sequenceName);

        TypedLogger.LogSequenceGenerationStarted(this.logger, sequenceName, schema ?? "default", this.contextTypeName);

        if (!await semaphore.WaitAsync(lockTimeout, cancellationToken))
        {
            TypedLogger.LogSequenceLockTimeout(this.logger, sequenceName, lockTimeout.TotalSeconds);

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
                TypedLogger.LogSequenceGenerated(this.logger, result.Value, sequenceName, schema ?? "default", this.contextTypeName);
            }
            else
            {
                TypedLogger.LogSequenceGenerationFailed(this.logger, sequenceName, schema ?? "default", this.contextTypeName);
            }

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

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

        TypedLogger.LogMultipleSequenceGenerationStarted(this.logger, names.Count, schema ?? "default", this.contextTypeName);

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

        TypedLogger.LogMultipleSequenceGenerated(this.logger, names.Count, this.contextTypeName);

        return Result<Dictionary<string, long>>.Success(results);
    }

    public Task<Result<long>> GetNextForEntityAsync<TEntity>(
        string schema = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entityName = typeof(TEntity).Name;
        var sequenceName = $"{entityName}Sequence";

        TypedLogger.LogEntitySequenceGeneration(this.logger, entityName, sequenceName);

        return this.GetNextAsync(sequenceName, schema, cancellationToken);
    }

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

    public async Task<Result> ResetSequenceAsync(
        string sequenceName,
        long startValue,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(sequenceName, schema);
        var semaphore = this.sequenceLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        var lockTimeout = this.GetLockTimeout(sequenceName);

        TypedLogger.LogSequenceResetStarted(this.logger, sequenceName, startValue, this.contextTypeName);

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

            if (result.IsSuccess)
            {
                TypedLogger.LogSequenceReset(this.logger, sequenceName, startValue, this.contextTypeName);
            }

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    protected abstract Task<Result<long>> GetNextInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    protected abstract Task<Result<bool>> ExistsInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    protected abstract Task<Result<SequenceInfo>> GetSequenceInfoInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

    protected abstract Task<Result<long>> GetCurrentValueInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken);

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

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug,
            "Sequence generation started (sequence={SequenceName}, schema={Schema}, context={Context})")]
        public static partial void LogSequenceGenerationStarted(
            ILogger logger, string sequenceName, string schema, string context);

        [LoggerMessage(1, LogLevel.Debug,
            "Generated sequence value {Value} from {SequenceName} (schema={Schema}, context={Context})")]
        public static partial void LogSequenceGenerated(
            ILogger logger, long value, string sequenceName, string schema, string context);

        [LoggerMessage(2, LogLevel.Error,
            "Sequence generation failed (sequence={SequenceName}, schema={Schema}, context={Context})")]
        public static partial void LogSequenceGenerationFailed(
            ILogger logger, string sequenceName, string schema, string context);

        [LoggerMessage(3, LogLevel.Warning,
            "Failed to acquire lock for sequence {SequenceName} within {Timeout} seconds")]
        public static partial void LogSequenceLockTimeout(
            ILogger logger, string sequenceName, double timeout);

        [LoggerMessage(4, LogLevel.Debug,
            "Multiple sequence generation started (count={Count}, schema={Schema}, context={Context})")]
        public static partial void LogMultipleSequenceGenerationStarted(
            ILogger logger, int count, string schema, string context);

        [LoggerMessage(5, LogLevel.Debug,
            "Multiple sequences generated (count={Count}, context={Context})")]
        public static partial void LogMultipleSequenceGenerated(
            ILogger logger, int count, string context);

        [LoggerMessage(6, LogLevel.Debug,
            "Entity sequence generation (entity={EntityName}, sequence={SequenceName})")]
        public static partial void LogEntitySequenceGeneration(
            ILogger logger, string entityName, string sequenceName);

        [LoggerMessage(7, LogLevel.Information,
            "Sequence reset started (sequence={SequenceName}, startValue={StartValue}, context={Context})")]
        public static partial void LogSequenceResetStarted(
            ILogger logger, string sequenceName, long startValue, string context);

        [LoggerMessage(8, LogLevel.Information,
            "Sequence reset completed (sequence={SequenceName}, startValue={StartValue}, context={Context})")]
        public static partial void LogSequenceReset(
            ILogger logger, string sequenceName, long startValue, string context);
    }
}