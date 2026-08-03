// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures per-client blob-store behavior and provider limits.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobStoreOptions
/// {
///     MaxBlobSize = ByteSize.Megabytes(50),
///     AllowFullScans = true
/// };
/// </code>
/// </example>
public sealed class BlobStoreOptions
{
    /// <summary>
    /// Gets or sets the default listing page size.
    /// </summary>
    /// <example>
    /// <code>
    /// options.DefaultTake = 100;
    /// </code>
    /// </example>
    public int DefaultTake { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum listing page size.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxTake = 1000;
    /// </code>
    /// </example>
    public int MaxTake { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the optional abstraction-level maximum blob size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxBlobSize = ByteSize.Megabytes(50);
    /// </code>
    /// </example>
    public long? MaxBlobSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether full container scans are globally allowed.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AllowFullScans = true;
    /// </code>
    /// </example>
    public bool AllowFullScans { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each full scan query must opt in explicitly.
    /// </summary>
    /// <example>
    /// <code>
    /// options.RequireExplicitFullScanApproval = true;
    /// </code>
    /// </example>
    public bool RequireExplicitFullScanApproval { get; set; } = true;

    /// <summary>
    /// Gets or sets the EF Core chunk size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ChunkSize = (int)ByteSize.Megabytes(4);
    /// </code>
    /// </example>
    public int ChunkSize { get; set; } = (int)ByteSize.Megabytes(4);

    /// <summary>
    /// Gets or sets the internal lease duration used by providers that require leases.
    /// </summary>
    /// <example>
    /// <code>
    /// options.LeaseDuration = TimeSpan.FromMinutes(1);
    /// </code>
    /// </example>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the optional logical lease owner.
    /// </summary>
    /// <example>
    /// <code>
    /// options.LeaseOwner = "worker-a";
    /// </code>
    /// </example>
    public string LeaseOwner { get; set; }

    /// <summary>
    /// Validates option values.
    /// </summary>
    /// <returns>A success result when option values are valid.</returns>
    /// <example>
    /// <code>
    /// var validation = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        if (this.DefaultTake <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("DefaultTake must be greater than zero."));
        }

        if (this.MaxTake <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("MaxTake must be greater than zero."));
        }

        if (this.DefaultTake > this.MaxTake)
        {
            return Result.Failure(new BlobStoreValidationError("DefaultTake must be less than or equal to MaxTake."));
        }

        if (this.MaxBlobSize is <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("MaxBlobSize must be greater than zero when configured."));
        }

        if (this.ChunkSize <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("ChunkSize must be greater than zero."));
        }

        if (this.LeaseDuration <= TimeSpan.Zero)
        {
            return Result.Failure(new BlobStoreValidationError("LeaseDuration must be greater than zero."));
        }

        return Result.Success();
    }
}
