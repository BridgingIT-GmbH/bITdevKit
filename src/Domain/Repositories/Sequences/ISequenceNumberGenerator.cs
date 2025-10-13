// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Generates sequence numbers from database sequences with thread-safe operations.
/// </summary>
public interface ISequenceNumberGenerator
{
    /// <summary>
    /// Gets the next value from the specified sequence.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the next sequence value or errors.</returns>
    Task<Result<long>> GetNextAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next values from multiple sequences in a single operation.
    /// </summary>
    /// <param name="sequenceNames">The names of the sequences.</param>
    /// <param name="schema">The schema containing the sequences (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing a dictionary of sequence names and their next values.</returns>
    Task<Result<Dictionary<string, long>>> GetNextMultipleAsync(
        IEnumerable<string> sequenceNames,
        string schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next value for a sequence based on entity type convention.
    /// Convention: {EntityName}Sequence
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the next sequence value or errors.</returns>
    Task<Result<long>> GetNextForEntityAsync<TEntity>(
        string schema = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    /// <summary>
    /// Checks if the specified sequence exists.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether the sequence exists.</returns>
    Task<Result<bool>> ExistsAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata information about the specified sequence.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing sequence metadata information.</returns>
    Task<Result<SequenceInfo>> GetSequenceInfoAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current value of the sequence without incrementing it.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the current sequence value.</returns>
    Task<Result<long>> GetCurrentValueAsync(
        string sequenceName,
        string schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the sequence to the specified start value.
    /// </summary>
    /// <param name="sequenceName">The name of the sequence.</param>
    /// <param name="startValue">The value to reset the sequence to.</param>
    /// <param name="schema">The schema containing the sequence (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure of the reset operation.</returns>
    Task<Result> ResetSequenceAsync(
        string sequenceName,
        long startValue,
        string schema = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for sequence number generation.
/// </summary>
public class SequenceNumberGeneratorOptions
{
    /// <summary>
    /// Gets or sets the timeout duration for acquiring sequence locks.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout duration for database operations.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the minimum log level for sequence operations.
    /// Default is Information.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets per-sequence configuration overrides.
    /// Key is the sequence name, value is the sequence-specific options.
    /// </summary>
    public Dictionary<string, SequenceOptions> SequenceOverrides { get; set; } = [];
}

/// <summary>
/// Configuration options for a specific sequence.
/// </summary>
public class SequenceOptions
{
    /// <summary>
    /// Gets or sets the lock timeout for this specific sequence.
    /// Overrides the global LockTimeout setting.
    /// </summary>
    public TimeSpan? LockTimeout { get; set; }

    /// <summary>
    /// Gets or sets the operation timeout for this specific sequence.
    /// Overrides the global OperationTimeout setting.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }
}

/// <summary>
/// Contains metadata information about a database sequence.
/// </summary>
public class SequenceInfo
{
    /// <summary>
    /// Gets or sets the name of the sequence.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the schema containing the sequence.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the current value of the sequence.
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the minimum value the sequence can generate.
    /// </summary>
    public long MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value the sequence can generate.
    /// </summary>
    public long MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the increment step for the sequence.
    /// </summary>
    public int Increment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sequence cycles back to minimum after reaching maximum.
    /// </summary>
    public bool IsCyclic { get; set; }
}

/// <summary>
/// Error indicating that a sequence was not found in the database.
/// </summary>
public class SequenceNotFoundError(string sequenceName, string schema) : ResultErrorBase($"Sequence '{sequenceName}' not found in schema '{schema}'")
{
    /// <summary>
    /// Gets the name of the sequence that was not found.
    /// </summary>
    public string SequenceName { get; } = sequenceName;

    /// <summary>
    /// Gets the schema where the sequence was expected.
    /// </summary>
    public string Schema { get; } = schema;
}

/// <summary>
/// Error indicating that a sequence lock could not be acquired within the timeout period.
/// </summary>
public class SequenceLockTimeoutError(string sequenceName, TimeSpan timeout) : ResultErrorBase($"Failed to acquire lock for sequence '{sequenceName}' within {timeout.TotalSeconds} seconds")
{
    /// <summary>
    /// Gets the name of the sequence that could not be locked.
    /// </summary>
    public string SequenceName { get; } = sequenceName;

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; } = timeout;
}
