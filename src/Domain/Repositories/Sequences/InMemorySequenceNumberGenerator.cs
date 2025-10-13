// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// In-memory implementation of sequence number generator for testing purposes.
/// Thread-safe and provides deterministic sequence generation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemorySequenceNumberGenerator"/> class.
/// </remarks>
/// <param name="logger">Optional logger instance.</param>
public class InMemorySequenceNumberGenerator(
    ILogger<InMemorySequenceNumberGenerator> logger = null) : ISequenceNumberGenerator
{
    private readonly ConcurrentDictionary<string, SequenceState> sequences = [];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = [];

    /// <summary>
    /// Configures a sequence with specific settings.
    /// Useful for setting up test scenarios with known sequence values.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="startValue">The starting value.</param>
    /// <param name="increment">The increment step.</param>
    /// <param name="minValue">The minimum value.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <param name="isCyclic">Whether the sequence cycles.</param>
    /// <param name="schema">The schema (optional).</param>
    public void ConfigureSequence(
        string sequenceName,
        long startValue = 1,
        int increment = 1,
        long minValue = 1,
        long maxValue = long.MaxValue,
        bool isCyclic = false,
        string schema = null)
    {
        var key = GetKey(sequenceName, schema);
        this.sequences[key] = new SequenceState
        {
            Name = sequenceName,
            Schema = schema ?? "default",
            CurrentValue = startValue - increment,
            MinValue = minValue,
            MaxValue = maxValue,
            Increment = increment,
            IsCyclic = isCyclic
        };
    }

    public async Task<Result<long>> GetNextAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(sequenceName, schema);
        var semaphore = this.locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (!this.sequences.TryGetValue(key, out var foundState))
        {
            return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default"));
        }

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var state = this.sequences.GetOrAdd(key, _ => new SequenceState
            {
                Name = sequenceName,
                Schema = schema ?? "default",
                CurrentValue = 0,
                MinValue = 1,
                MaxValue = long.MaxValue,
                Increment = 1,
                IsCyclic = false
            });

            var nextValue = state.CurrentValue + state.Increment;
            if (nextValue > state.MaxValue)
            {
                if (state.IsCyclic)
                {
                    nextValue = state.MinValue;
                }
                else
                {
                    return Result<long>.Failure()
                        .WithMessage($"Sequence '{sequenceName}' has exceeded maximum value");
                }
            }

            state.CurrentValue = nextValue;

            logger?.LogDebug(
                "Generated in-memory sequence value {Value} for {Sequence}",
                nextValue,
                key);

            return Result<long>.Success(nextValue);
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
        var results = new Dictionary<string, long>();
        var errors = new List<IResultError>();

        foreach (var name in sequenceNames)
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

    public Task<Result<bool>> ExistsAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(sequenceName, schema);
        var exists = this.sequences.ContainsKey(key);

        return Task.FromResult(Result<bool>.Success(exists));
    }

    public Task<Result<SequenceInfo>> GetSequenceInfoAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(sequenceName, schema);

        if (!this.sequences.TryGetValue(key, out var state))
        {
            return Task.FromResult(
                Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default")));
        }

        var info = new SequenceInfo
        {
            Name = state.Name,
            Schema = state.Schema,
            CurrentValue = state.CurrentValue,
            MinValue = state.MinValue,
            MaxValue = state.MaxValue,
            Increment = state.Increment,
            IsCyclic = state.IsCyclic
        };

        return Task.FromResult(Result<SequenceInfo>.Success(info));
    }

    public Task<Result<long>> GetCurrentValueAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(sequenceName, schema);

        if (!this.sequences.TryGetValue(key, out var state))
        {
            return Task.FromResult(
                Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(
                        sequenceName,
                        schema ?? "default")));
        }

        return Task.FromResult(Result<long>.Success(state.CurrentValue));
    }

    public Task<Result> ResetSequenceAsync(
        string sequenceName,
        long startValue,
        string schema = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(sequenceName, schema);

        if (!this.sequences.TryGetValue(key, out var state))
        {
            return Task.FromResult(
                Result.Failure()
                    .WithError(new SequenceNotFoundError(
                        sequenceName,
                        schema ?? "default")));
        }

        state.CurrentValue = startValue - state.Increment;

        logger?.LogDebug(
            "Reset in-memory sequence {Sequence} to {Value}",
            key,
            startValue);

        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Clears all configured sequences. Useful for test cleanup.
    /// </summary>
    public void Clear()
    {
        this.sequences.Clear();
        this.locks.Clear();
    }

    private static string GetKey(string sequenceName, string schema)
    {
        return string.IsNullOrWhiteSpace(schema)
            ? sequenceName
            : $"{schema}.{sequenceName}";
    }

    private class SequenceState
    {
        public string Name { get; set; }
        public string Schema { get; set; }
        public long CurrentValue { get; set; }
        public long MinValue { get; set; }
        public long MaxValue { get; set; }
        public int Increment { get; set; }
        public bool IsCyclic { get; set; }
    }
}