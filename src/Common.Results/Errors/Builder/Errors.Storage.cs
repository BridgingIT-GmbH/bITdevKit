// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
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
    }
}

/// <summary>
/// Represents a general file system or blob storage error.
/// </summary>
/// <param name="message">The error message that describes the storage error. If null, a default message is used.</param>
public class StorageError(string message = null) : ResultErrorBase(message ?? "Storage error")
{
    public StorageError() : this(null)
    {
    }
}