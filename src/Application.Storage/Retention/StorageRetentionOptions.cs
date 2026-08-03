// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures hosted storage-retention sweeping.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage(options => options.WithRetention(retention =>
/// {
///     retention.SweepInterval = TimeSpan.FromHours(1);
///     retention.BatchSize = 500;
/// }));
/// </code>
/// </example>
public sealed class StorageRetentionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether hosted retention sweeping is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Enabled = true;
    /// </code>
    /// </example>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay before the first background sweep starts.
    /// </summary>
    /// <example>
    /// <code>
    /// options.StartupDelay = TimeSpan.FromSeconds(15);
    /// </code>
    /// </example>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the interval between background retention sweeps.
    /// </summary>
    /// <example>
    /// <code>
    /// options.SweepInterval = TimeSpan.FromHours(1);
    /// </code>
    /// </example>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of expired blobs a provider should delete in one batch.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BatchSize = 1000;
    /// </code>
    /// </example>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of batches to process for one store during a sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxBatchesPerStore = 10;
    /// </code>
    /// </example>
    public int MaxBatchesPerStore { get; set; } = 10;

    /// <summary>
    /// Gets or sets the delay between provider-side delete batches.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BatchDelay = TimeSpan.FromMilliseconds(100);
    /// </code>
    /// </example>
    public TimeSpan BatchDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the graceful shutdown wait for the background service.
    /// </summary>
    /// <example>
    /// <code>
    /// options.StopTimeout = TimeSpan.FromSeconds(10);
    /// </code>
    /// </example>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Validates option values.
    /// </summary>
    /// <returns>A success result when the options are valid.</returns>
    /// <example>
    /// <code>
    /// var validation = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        if (this.StartupDelay < TimeSpan.Zero)
        {
            return Result.Failure(new ValidationError("Retention StartupDelay must not be negative."));
        }

        if (this.SweepInterval <= TimeSpan.Zero)
        {
            return Result.Failure(new ValidationError("Retention SweepInterval must be greater than zero."));
        }

        if (this.BatchSize <= 0)
        {
            return Result.Failure(new ValidationError("Retention BatchSize must be greater than zero."));
        }

        if (this.MaxBatchesPerStore <= 0)
        {
            return Result.Failure(new ValidationError("Retention MaxBatchesPerStore must be greater than zero."));
        }

        if (this.BatchDelay < TimeSpan.Zero)
        {
            return Result.Failure(new ValidationError("Retention BatchDelay must not be negative."));
        }

        if (this.StopTimeout <= TimeSpan.Zero)
        {
            return Result.Failure(new ValidationError("Retention StopTimeout must be greater than zero."));
        }

        return Result.Success();
    }
}
