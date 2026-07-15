// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Configures provider-neutral behavior for explicit entity bulk insert operations.
/// </summary>
/// <example>
/// <code>
/// var options = new EntityBulkInsertOptions
/// {
///     BatchSize = 1_000,
///     CommandTimeout = 120,
///     KeepGeneratedIdentityValues = true
/// };
/// </code>
/// </example>
public class EntityBulkInsertOptions
{
    /// <summary>
    /// Gets or sets the maximum number of rows sent to the database in one provider batch.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BatchSize = 5_000;
    /// </code>
    /// </example>
    public int BatchSize { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the provider command timeout in seconds. A value of zero lets the provider use an unlimited timeout when it supports one.
    /// </summary>
    /// <example>
    /// <code>
    /// options.CommandTimeout = 120;
    /// </code>
    /// </example>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether generated GUID key properties with their default value receive sequential client-generated values before insertion.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AssignSequentialGuidKeys = true;
    /// </code>
    /// </example>
    public bool AssignSequentialGuidKeys { get; set; } = true;

    /// <summary>
    /// Gets or sets whether entities implementing the concurrency contract receive a new concurrency version before insertion.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AssignConcurrencyVersions = true;
    /// </code>
    /// </example>
    public bool AssignConcurrencyVersions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether caller-supplied values for store-generated identity columns are included in the prepared insert batch.
    /// </summary>
    /// <example>
    /// <code>
    /// options.KeepGeneratedIdentityValues = true;
    /// </code>
    /// </example>
    public bool KeepGeneratedIdentityValues { get; set; }

    internal void Validate()
    {
        if (this.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.BatchSize), this.BatchSize, "Batch size must be greater than zero.");
        }

        if (this.CommandTimeout < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.CommandTimeout), this.CommandTimeout, "Command timeout must not be negative.");
        }
    }
}
