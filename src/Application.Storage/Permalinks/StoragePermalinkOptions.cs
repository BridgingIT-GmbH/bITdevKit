// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures asynchronous permalink synchronization.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinks(options => options.QueueCapacity = 8192);
/// </code>
/// </example>
public sealed class StoragePermalinkOptions
{
    /// <summary>
    /// Gets or sets the bounded change queue capacity.
    /// </summary>
    public int QueueCapacity { get; set; } = 4096;

    /// <summary>
    /// Gets or sets how long a completed storage operation waits for queue capacity.
    /// </summary>
    public TimeSpan EnqueueTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long shutdown waits to drain queued changes.
    /// </summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of registry handling attempts.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Validates the options.
    /// </summary>
    public Result Validate() => this.QueueCapacity <= 0 || this.EnqueueTimeout <= TimeSpan.Zero || this.ShutdownDrainTimeout <= TimeSpan.Zero || this.RetryAttempts <= 0
        ? Result.Failure(new StoragePermalinkValidationError("Permalink queue capacity, timeouts, and retry attempts must be positive."))
        : Result.Success();
}
