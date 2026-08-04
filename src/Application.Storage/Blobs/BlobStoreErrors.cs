// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a blob-store validation failure.
/// </summary>
/// <param name="message">The validation failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreValidationError("Container is required.");
/// </code>
/// </example>
public sealed class BlobStoreValidationError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents a missing blob failure.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreNotFoundError(new BlobKey("reports", "missing.pdf"));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreNotFoundError" /> class.
/// </remarks>
/// <param name="key">The missing blob key.</param>
/// <example>
/// <code>
/// var error = new BlobStoreNotFoundError(new BlobKey("reports", "missing.pdf"));
/// </code>
/// </example>
public sealed class BlobStoreNotFoundError(BlobKey key) : ResultErrorBase($"Blob with container '{key?.Container}' and name '{key?.Name}' was not found.")
{

    /// <summary>
    /// Gets the missing blob key.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = error.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; } = key;
}

/// <summary>
/// Represents a blob query that is too broad for the current options.
/// </summary>
/// <param name="message">The query failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreQueryTooBroadError("Full scans are disabled.");
/// </code>
/// </example>
public sealed class BlobStoreQueryTooBroadError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents a requested blob page size that exceeds the configured maximum.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStorePageSizeExceededError(1001, 1000);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStorePageSizeExceededError" /> class.
/// </remarks>
/// <param name="take">The requested page size.</param>
/// <param name="maxTake">The configured maximum page size.</param>
/// <example>
/// <code>
/// var error = new BlobStorePageSizeExceededError(1001, 1000);
/// </code>
/// </example>
public sealed class BlobStorePageSizeExceededError(int take, int maxTake) : ResultErrorBase($"Requested blob page size {take} exceeds the maximum page size {maxTake}.")
{

    /// <summary>
    /// Gets the requested page size.
    /// </summary>
    /// <example>
    /// <code>
    /// var take = error.Take;
    /// </code>
    /// </example>
    public int Take { get; } = take;

    /// <summary>
    /// Gets the configured maximum page size.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxTake = error.MaxTake;
    /// </code>
    /// </example>
    public int MaxTake { get; } = maxTake;
}

/// <summary>
/// Represents a blob query shape that is unsupported by the provider.
/// </summary>
/// <param name="message">The unsupported query failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreQueryNotSupportedError("Prefix listing is not supported.");
/// </code>
/// </example>
public sealed class BlobStoreQueryNotSupportedError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents an invalid blob continuation token.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreInvalidContinuationTokenError("Continuation token is invalid.");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreInvalidContinuationTokenError" /> class.
/// </remarks>
/// <param name="message">The continuation token failure message.</param>
/// <param name="innerException">The optional exception that caused the token failure.</param>
/// <example>
/// <code>
/// var error = new BlobStoreInvalidContinuationTokenError("Continuation token is invalid.", exception);
/// </code>
/// </example>
public sealed class BlobStoreInvalidContinuationTokenError(string message, Exception innerException = null) : ResultErrorBase(message)
{

    /// <summary>
    /// Gets the optional exception that caused the token failure.
    /// </summary>
    /// <example>
    /// <code>
    /// var exception = error.InnerException;
    /// </code>
    /// </example>
    public Exception InnerException { get; } = innerException;
}

/// <summary>
/// Represents a blob operation conflict.
/// </summary>
/// <param name="message">The conflict failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreConflictError("Blob already exists.");
/// </code>
/// </example>
public sealed class BlobStoreConflictError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents an internal blob lease failure.
/// </summary>
/// <param name="message">The lease failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreLeaseError("Could not acquire blob lease.");
/// </code>
/// </example>
public sealed class BlobStoreLeaseError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents a blob property or content serialization failure.
/// </summary>
/// <param name="message">The serialization failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreSerializationError("Properties could not be serialized.");
/// </code>
/// </example>
public sealed class BlobStoreSerializationError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents a provider-specific blob storage failure.
/// </summary>
/// <param name="message">The provider failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreProviderError("Provider request failed.");
/// </code>
/// </example>
public sealed class BlobStoreProviderError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents a provider mutation whose compensating restore also failed.
/// </summary>
/// <param name="message">The partial-update failure message.</param>
/// <param name="operationError">The original operation error.</param>
/// <param name="restoreError">The compensating restore error.</param>
/// <example>
/// <code>
/// var error = new BlobStorePartialUpdateError("Property update could not be restored.", operation, restore);
/// </code>
/// </example>
public sealed class BlobStorePartialUpdateError(
    string message,
    string operationError,
    string restoreError) : ResultErrorBase(message)
{
    /// <summary>
    /// Gets the original operation error.
    /// </summary>
    /// <example>
    /// <code>
    /// var operation = error.OperationError;
    /// </code>
    /// </example>
    public string OperationError { get; } = operationError;

    /// <summary>
    /// Gets the compensating restore error.
    /// </summary>
    /// <example>
    /// <code>
    /// var restore = error.RestoreError;
    /// </code>
    /// </example>
    public string RestoreError { get; } = restoreError;
}

/// <summary>
/// Represents a blob transfer failure that may include partial transfer state.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreTransferError("Move delete failed.", sourceKey, targetKey, true, false);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreTransferError" /> class.
/// </remarks>
/// <param name="message">The transfer failure message.</param>
/// <param name="sourceKey">The source blob key when known.</param>
/// <param name="targetKey">The target blob key when known.</param>
/// <param name="copySucceeded">A value indicating whether the copy step succeeded.</param>
/// <param name="deleteSucceeded">A value indicating whether the delete step succeeded.</param>
/// <param name="source">The source blob metadata when known.</param>
/// <param name="target">The target blob metadata when known.</param>
/// <example>
/// <code>
/// var error = new BlobStoreTransferError("Source delete failed.", sourceKey, targetKey, true, false, source, target);
/// </code>
/// </example>
public sealed class BlobStoreTransferError(
    string message,
    BlobKey sourceKey,
    BlobKey targetKey,
    bool copySucceeded,
    bool deleteSucceeded,
    BlobInfo source = null,
    BlobInfo target = null) : ResultErrorBase(message)
{


    /// <summary>
    /// Gets the source blob key when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var sourceKey = error.SourceKey;
    /// </code>
    /// </example>
    public BlobKey SourceKey { get; } = sourceKey;

    /// <summary>
    /// Gets the target blob key when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var targetKey = error.TargetKey;
    /// </code>
    /// </example>
    public BlobKey TargetKey { get; } = targetKey;

    /// <summary>
    /// Gets a value indicating whether the copy step succeeded.
    /// </summary>
    /// <example>
    /// <code>
    /// var copied = error.CopySucceeded;
    /// </code>
    /// </example>
    public bool CopySucceeded { get; } = copySucceeded;

    /// <summary>
    /// Gets a value indicating whether the delete step succeeded.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = error.DeleteSucceeded;
    /// </code>
    /// </example>
    public bool DeleteSucceeded { get; } = deleteSucceeded;

    /// <summary>
    /// Gets the source blob metadata when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = error.Source;
    /// </code>
    /// </example>
    public BlobInfo Source { get; } = source;

    /// <summary>
    /// Gets the target blob metadata when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var target = error.Target;
    /// </code>
    /// </example>
    public BlobInfo Target { get; } = target;
}

/// <summary>
/// Represents a blob upload that exceeds the configured maximum size.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreSizeLimitExceededError(1025, 1024);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreSizeLimitExceededError" /> class.
/// </remarks>
/// <param name="actualSize">The observed blob size in bytes.</param>
/// <param name="maxSize">The configured maximum blob size in bytes.</param>
/// <example>
/// <code>
/// var error = new BlobStoreSizeLimitExceededError(1025, 1024);
/// </code>
/// </example>
public sealed class BlobStoreSizeLimitExceededError(long actualSize, long maxSize) : ResultErrorBase($"Blob size {actualSize} bytes exceeds the configured maximum blob size {maxSize} bytes.")
{

    /// <summary>
    /// Gets the observed blob size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var actualSize = error.ActualSize;
    /// </code>
    /// </example>
    public long ActualSize { get; } = actualSize;

    /// <summary>
    /// Gets the configured maximum blob size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxSize = error.MaxSize;
    /// </code>
    /// </example>
    public long MaxSize { get; } = maxSize;
}

/// <summary>
/// Represents a blob content integrity failure.
/// </summary>
/// <param name="message">The integrity failure message.</param>
/// <example>
/// <code>
/// var error = new BlobStoreIntegrityError("Expected content hash did not match.");
/// </code>
/// </example>
public sealed class BlobStoreIntegrityError(string message) : ResultErrorBase(message);

/// <summary>
/// Represents an upload rejected because the bounded waiting queue is full.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreUploadOverloadedError("reports", 4, 16);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreUploadOverloadedError" /> class.
/// </remarks>
/// <param name="storeName">The normalized blob-store name.</param>
/// <param name="maxConcurrentUploads">The configured active upload limit.</param>
/// <param name="maxQueuedUploads">The configured waiting upload limit.</param>
public sealed class BlobStoreUploadOverloadedError(
    string storeName,
    int maxConcurrentUploads,
    int maxQueuedUploads)
    : ResultErrorBase(
        $"Blob store '{storeName}' cannot admit another upload because its bounded queue is full " +
        $"(active limit: {maxConcurrentUploads}, queue limit: {maxQueuedUploads}).")
{
    /// <summary>
    /// Gets the normalized blob-store name.
    /// </summary>
    /// <example>
    /// <code>
    /// var storeName = error.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; } = storeName;

    /// <summary>
    /// Gets the configured active upload limit.
    /// </summary>
    /// <example>
    /// <code>
    /// var activeLimit = error.MaxConcurrentUploads;
    /// </code>
    /// </example>
    public int MaxConcurrentUploads { get; } = maxConcurrentUploads;

    /// <summary>
    /// Gets the configured waiting upload limit.
    /// </summary>
    /// <example>
    /// <code>
    /// var queueLimit = error.MaxQueuedUploads;
    /// </code>
    /// </example>
    public int MaxQueuedUploads { get; } = maxQueuedUploads;
}

/// <summary>
/// Represents an upload whose bounded admission wait expired.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreUploadAdmissionTimeoutError("reports", TimeSpan.FromSeconds(30));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreUploadAdmissionTimeoutError" /> class.
/// </remarks>
/// <param name="storeName">The normalized blob-store name.</param>
/// <param name="queueWaitTimeout">The configured admission wait timeout.</param>
public sealed class BlobStoreUploadAdmissionTimeoutError(
    string storeName,
    TimeSpan queueWaitTimeout)
    : ResultErrorBase(
        $"Blob store '{storeName}' could not admit the upload within {queueWaitTimeout}.")
{
    /// <summary>
    /// Gets the normalized blob-store name.
    /// </summary>
    /// <example>
    /// <code>
    /// var storeName = error.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; } = storeName;

    /// <summary>
    /// Gets the configured admission wait timeout.
    /// </summary>
    /// <example>
    /// <code>
    /// var timeout = error.QueueWaitTimeout;
    /// </code>
    /// </example>
    public TimeSpan QueueWaitTimeout { get; } = queueWaitTimeout;
}

/// <summary>
/// Represents a blob operation timeout.
/// </summary>
/// <example>
/// <code>
/// var error = new BlobStoreTimeoutError("upload", TimeSpan.FromSeconds(30));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobStoreTimeoutError" /> class.
/// </remarks>
/// <param name="operation">The timed-out blob operation name.</param>
/// <param name="timeout">The configured timeout.</param>
/// <example>
/// <code>
/// var error = new BlobStoreTimeoutError("download", TimeSpan.FromSeconds(30));
/// </code>
/// </example>
public sealed class BlobStoreTimeoutError(string operation, TimeSpan timeout) : ResultErrorBase($"Blob storage operation '{operation}' timed out after {timeout}.")
{


    /// <summary>
    /// Gets the timed-out blob operation name.
    /// </summary>
    /// <example>
    /// <code>
    /// var operation = error.Operation;
    /// </code>
    /// </example>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets the configured timeout.
    /// </summary>
    /// <example>
    /// <code>
    /// var timeout = error.Timeout;
    /// </code>
    /// </example>
    public TimeSpan Timeout { get; } = timeout;
}
