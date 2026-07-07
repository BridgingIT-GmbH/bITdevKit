// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides factory methods for common result errors.
/// </summary>
public static partial class Errors
{
    /// <summary>
    /// Provides factory methods for storage-related result errors.
    /// </summary>
    public static partial class Storage
    {
        /// <summary>Creates a <see cref="FileSystemError"/> for file system operation failures.</summary>
        public static FileSystemError FileSystem(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="FileSystemPermissionError"/> for file access permission issues.</summary>
        public static FileSystemPermissionError FileSystemPermission(string path, string message = null, Exception innerException = null)
            => new(message, path, innerException);

        /// <summary>Creates a <see cref="BlobStorageError"/> for blob storage operation failures.</summary>
        public static BlobStorageError BlobStorage(string path, string message = null, int? statusCode = null, Exception innerException = null)
            => new(message, path, statusCode, innerException);

        /// <summary>Creates a <see cref="StorageError"/> for general file system and blob storage errors.</summary>
        public static StorageError Error(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreInvalidQueryError"/> for invalid document query shapes.</summary>
        public static DocumentStoreInvalidQueryError DocumentInvalidQuery(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStorePageSizeExceededError"/> for page size limit violations.</summary>
        public static DocumentStorePageSizeExceededError DocumentPageSizeExceeded(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreFullScanNotAllowedError"/> for blocked full scans.</summary>
        public static DocumentStoreFullScanNotAllowedError DocumentFullScanNotAllowed(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreQueryNotSupportedError"/> for unsupported provider query shapes.</summary>
        public static DocumentStoreQueryNotSupportedError DocumentQueryNotSupported(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreClientSideFilteringRejectedError"/> for rejected client-side filtering.</summary>
        public static DocumentStoreClientSideFilteringRejectedError DocumentClientSideFilteringRejected(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreInvalidContinuationTokenError"/> for invalid continuation tokens.</summary>
        public static DocumentStoreInvalidContinuationTokenError DocumentInvalidContinuationToken(string message = null, Exception innerException = null)
            => new(message, innerException);

        /// <summary>Creates a <see cref="DocumentStoreContinuationTokenQueryMismatchError"/> for continuation token query mismatches.</summary>
        public static DocumentStoreContinuationTokenQueryMismatchError DocumentContinuationTokenQueryMismatch(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreNotFoundError"/> for missing documents.</summary>
        public static DocumentStoreNotFoundError DocumentNotFound(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="DocumentStoreProviderError"/> for provider failures.</summary>
        public static DocumentStoreProviderError DocumentProvider(string message = null, Exception innerException = null)
            => new(message, innerException);

        /// <summary>Creates a <see cref="DocumentStoreSerializationError"/> for serialization failures.</summary>
        public static DocumentStoreSerializationError DocumentSerialization(string message = null, Exception innerException = null)
            => new(message, innerException);
    }
}

/// <summary>
/// Represents a general file system or blob storage error.
/// </summary>
/// <param name="message">The error message that describes the storage error. If null, a default message is used.</param>
public class StorageError(string message = null) : ResultErrorBase(message ?? "Storage error")
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageError" /> class with the default message.
    /// </summary>
    public StorageError() : this(null)
    {
    }
}

/// <summary>
/// Represents an invalid document-store query error.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreInvalidQueryError(string message = null) : StorageError(message ?? "Document store query is invalid");

/// <summary>
/// Represents a document-store page size limit violation.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStorePageSizeExceededError(string message = null) : StorageError(message ?? "Document store page size exceeds the configured maximum");

/// <summary>
/// Represents a rejected document-store full scan.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreFullScanNotAllowedError(string message = null) : StorageError(message ?? "Document store full scan is not allowed");

/// <summary>
/// Represents an unsupported document-store query shape.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreQueryNotSupportedError(string message = null) : StorageError(message ?? "Document store query is not supported");

/// <summary>
/// Represents a rejected document-store query that would require client-side filtering.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreClientSideFilteringRejectedError(string message = null) : StorageError(message ?? "Document store client-side filtering is rejected");

/// <summary>
/// Represents an invalid document-store continuation token.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
/// <param name="innerException">The optional exception that caused the continuation token failure.</param>
public class DocumentStoreInvalidContinuationTokenError(string message = null, Exception innerException = null)
    : StorageError(message ?? "Document store continuation token is invalid")
{
    /// <summary>
    /// Gets the optional exception that caused the continuation token failure.
    /// </summary>
    public Exception InnerException { get; } = innerException;
}

/// <summary>
/// Represents a continuation token that does not match the current document-store query.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreContinuationTokenQueryMismatchError(string message = null)
    : StorageError(message ?? "Document store continuation token does not match the query");

/// <summary>
/// Represents a missing document error.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
public class DocumentStoreNotFoundError(string message = null) : StorageError(message ?? "Document was not found");

/// <summary>
/// Represents a provider-specific document-store failure.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
/// <param name="innerException">The optional exception that caused the provider failure.</param>
public class DocumentStoreProviderError(string message = null, Exception innerException = null)
    : StorageError(message ?? "Document store provider failed")
{
    /// <summary>
    /// Gets the optional exception that caused the provider failure.
    /// </summary>
    public Exception InnerException { get; } = innerException;
}

/// <summary>
/// Represents a document-store serialization failure.
/// </summary>
/// <param name="message">The error message. If null, a default message is used.</param>
/// <param name="innerException">The optional exception that caused the serialization failure.</param>
public class DocumentStoreSerializationError(string message = null, Exception innerException = null)
    : StorageError(message ?? "Document store serialization failed")
{
    /// <summary>
    /// Gets the optional exception that caused the serialization failure.
    /// </summary>
    public Exception InnerException { get; } = innerException;
}
