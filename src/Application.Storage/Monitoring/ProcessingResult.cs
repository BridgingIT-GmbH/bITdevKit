// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;

/// <summary>
/// Represents the result of processing a FileEvent by an IFileEventProcessor.
/// Captures the outcome (success/failure) and additional details for storage and auditing.
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Gets or sets the unique identifier for the processing result.
    /// Generated automatically when the result is created.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the identifier of the FileEvent this result pertains to.
    /// Links the result back to the original event.
    /// </summary>
    public Guid FileEventId { get; set; }

    /// <summary>
    /// Gets or sets the name of the processor that generated this result.
    /// Identifies which processor in the chain produced the outcome.
    /// </summary>
    public string ProcessorName { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was successful.
    /// True indicates success; false indicates failure.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a message describing the processing outcome.
    /// Provides context for success or details of failure (e.g., "File moved successfully" or "Move failed: access denied").
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the processing occurred.
    /// Records when the processor completed its action.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}