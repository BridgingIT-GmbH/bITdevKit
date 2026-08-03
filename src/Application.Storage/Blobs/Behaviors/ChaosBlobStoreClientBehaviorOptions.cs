// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures the chaos blob-store client behavior.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithChaosBehavior(options => options.FailDownloadsEvery = 3);
/// </code>
/// </example>
public sealed class ChaosBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether chaos injection is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Enabled = true;
    /// </code>
    /// </example>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the upload failure probability from 0.0 to 1.0.
    /// </summary>
    /// <example>
    /// <code>
    /// options.UploadFailureRate = 0.05;
    /// </code>
    /// </example>
    public double UploadFailureRate { get; set; }

    /// <summary>
    /// Gets or sets the download failure probability from 0.0 to 1.0.
    /// </summary>
    /// <example>
    /// <code>
    /// options.DownloadFailureRate = 0.05;
    /// </code>
    /// </example>
    public double DownloadFailureRate { get; set; }

    /// <summary>
    /// Gets or sets an optional deterministic upload failure interval.
    /// </summary>
    /// <example>
    /// <code>
    /// options.FailUploadsEvery = 2;
    /// </code>
    /// </example>
    public int? FailUploadsEvery { get; set; }

    /// <summary>
    /// Gets or sets an optional deterministic download failure interval.
    /// </summary>
    /// <example>
    /// <code>
    /// options.FailDownloadsEvery = 3;
    /// </code>
    /// </example>
    public int? FailDownloadsEvery { get; set; }

    /// <summary>
    /// Gets or sets the low-cardinality failure message returned in provider errors.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Message = "Injected blob storage chaos failure.";
    /// </code>
    /// </example>
    public string Message { get; set; } = "Injected blob storage chaos failure.";

    /// <summary>
    /// Gets or sets the random number source used for probabilistic failures.
    /// </summary>
    /// <example>
    /// <code>
    /// options.RandomDoubleFactory = () => 0.5;
    /// </code>
    /// </example>
    public Func<double> RandomDoubleFactory { get; set; } = Random.Shared.NextDouble;

    /// <summary>
    /// Validates the configured chaos options.
    /// </summary>
    /// <returns>A successful result when options are valid; otherwise a validation failure.</returns>
    /// <example>
    /// <code>
    /// var result = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        if (this.UploadFailureRate is < 0D or > 1D)
        {
            return Result.Failure(new BlobStoreValidationError("Upload chaos failure rate must be between 0.0 and 1.0."));
        }

        if (this.DownloadFailureRate is < 0D or > 1D)
        {
            return Result.Failure(new BlobStoreValidationError("Download chaos failure rate must be between 0.0 and 1.0."));
        }

        if (this.FailUploadsEvery is <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("Upload chaos failure interval must be greater than zero."));
        }

        if (this.FailDownloadsEvery is <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("Download chaos failure interval must be greater than zero."));
        }

        if (this.RandomDoubleFactory is null)
        {
            return Result.Failure(new BlobStoreValidationError("Chaos random double factory is required."));
        }

        return Result.Success();
    }
}
