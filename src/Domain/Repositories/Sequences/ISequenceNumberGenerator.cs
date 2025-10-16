// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

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
