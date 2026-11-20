// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Controls the bounded workers used by a blob upload batch.</summary>
/// <example><code>var options = new BlobUploadBatchOptions { MaxConcurrency = 4 };</code></example>
public sealed class BlobUploadBatchOptions
{
    /// <summary>The maximum number of requests accepted in one call.</summary>
    public const int MaximumItemCount = 1000;

    /// <summary>The maximum configurable number of active uploads in one call.</summary>
    public const int MaximumConcurrency = 64;

    /// <summary>Gets the maximum active uploads, from 1 through 64. The default is four.</summary>
    /// <remarks>Configured store admission can impose a lower effective limit.</remarks>
    /// <example><code>var sequential = new BlobUploadBatchOptions { MaxConcurrency = 1 };</code></example>
    public int MaxConcurrency { get; init; } = 4;
}

/// <summary>Contains the outcome of one request, at its original zero-based input position.</summary>
/// <example><code>if (item.Result.IsFailure) { Console.WriteLine(item.Index); }</code></example>
public sealed record BlobUploadOutcome
{
    /// <summary>Gets the original zero-based input position.</summary>
    public int Index
    {
        get; init;
    }

    /// <summary>Gets the submitted key, or null for an invalid request without a key.</summary>
    public BlobKey Key
    {
        get; init;
    }

    /// <summary>Gets the individual upload result, including any indeterminate mutation error.</summary>
    public Result<BlobInfo> Result
    {
        get; init;
    }
}

/// <summary>Contains every completed item outcome in input order, independently of execution order.</summary>
/// <remarks>A successful outer Result means the batch finished. Inspect item results or <see cref="FailedCount"/> for upload failures.</remarks>
/// <example><code>var failed = batch.Items.Where(item => item.Result.IsFailure);</code></example>
public sealed record BlobUploadBatchResult
{
    /// <summary>Gets the outcomes in original input order.</summary>
    public IReadOnlyList<BlobUploadOutcome> Items { get; init; } = [];

    /// <summary>Gets the number of acknowledged successful uploads.</summary>
    public int SucceededCount => this.Items.Count(item => item.Result.IsSuccess);

    /// <summary>Gets the number of item failures, including validation and indeterminate outcomes.</summary>
    public int FailedCount => this.Items.Count(item => item.Result.IsFailure);

    /// <summary>Gets whether every item succeeded. An empty batch also succeeds.</summary>
    public bool AllSucceeded => this.FailedCount == 0;
}