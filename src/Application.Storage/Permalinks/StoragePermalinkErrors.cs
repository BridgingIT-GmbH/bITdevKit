// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Reports invalid permalink input or configuration.
/// </summary>
public sealed class StoragePermalinkValidationError(string message) : ResultErrorBase(message);

/// <summary>
/// Reports that a permalink or resource mapping was not found.
/// </summary>
public sealed class StoragePermalinkNotFoundError(string message = null) : ResultErrorBase(message ?? "The permalink was not found.");

/// <summary>
/// Reports an optimistic-concurrency conflict.
/// </summary>
public sealed class StoragePermalinkConflictError(string message) : ResultErrorBase(message);

/// <summary>
/// Reports a registry-provider failure.
/// </summary>
public sealed class StoragePermalinkProviderError(string message, Exception innerException = null) : ResultErrorBase(message)
{
    /// <summary>
    /// Gets the provider exception when available.
    /// </summary>
    public Exception InnerException { get; } = innerException;
}

/// <summary>
/// Reports that a storage registration did not opt into permalink behavior.
/// </summary>
public sealed class StoragePermalinkNotEnabledError(string registrationName) : ResultErrorBase($"Storage registration '{registrationName}' does not have permalink behavior enabled.");
