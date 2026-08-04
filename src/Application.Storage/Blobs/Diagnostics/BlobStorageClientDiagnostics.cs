// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents diagnostics for one registered blob-store client.
/// </summary>
/// <example>
/// <code>
/// var healthy = diagnostics.IsHealthy;
/// </code>
/// </example>
public sealed class BlobStorageClientDiagnostics
{
    /// <summary>
    /// Gets the registered client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = diagnostics.Name;
    /// </code>
    /// </example>
    public string Name { get; init; }

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = diagnostics.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets the provider capabilities.
    /// </summary>
    /// <example>
    /// <code>
    /// var supportsPaging = diagnostics.Capabilities.SupportsContinuationPaging;
    /// </code>
    /// </example>
    public BlobStoreProviderCapabilities Capabilities { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether the non-mutating health probe succeeded.
    /// </summary>
    /// <example>
    /// <code>
    /// var healthy = diagnostics.IsHealthy;
    /// </code>
    /// </example>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Gets a readable probe status.
    /// </summary>
    /// <example>
    /// <code>
    /// var status = diagnostics.HealthStatus;
    /// </code>
    /// </example>
    public string HealthStatus { get; init; }

    /// <summary>
    /// Gets readable health details when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var details = diagnostics.HealthDetails;
    /// </code>
    /// </example>
    public string HealthDetails { get; init; }

    /// <summary>
    /// Gets a value indicating whether bounded upload admission is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// var enabled = diagnostics.UploadAdmissionEnabled;
    /// </code>
    /// </example>
    public bool UploadAdmissionEnabled { get; init; }

    /// <summary>
    /// Gets the configured maximum active upload count.
    /// </summary>
    /// <example>
    /// <code>
    /// var limit = diagnostics.MaxConcurrentUploads;
    /// </code>
    /// </example>
    public int MaxConcurrentUploads { get; init; }

    /// <summary>
    /// Gets the configured maximum queued upload count.
    /// </summary>
    /// <example>
    /// <code>
    /// var limit = diagnostics.MaxQueuedUploads;
    /// </code>
    /// </example>
    public int MaxQueuedUploads { get; init; }

    /// <summary>
    /// Gets the current active upload count.
    /// </summary>
    /// <example>
    /// <code>
    /// var active = diagnostics.ActiveUploads;
    /// </code>
    /// </example>
    public int ActiveUploads { get; init; }

    /// <summary>
    /// Gets the current queued upload count.
    /// </summary>
    /// <example>
    /// <code>
    /// var queued = diagnostics.QueuedUploads;
    /// </code>
    /// </example>
    public int QueuedUploads { get; init; }
}
