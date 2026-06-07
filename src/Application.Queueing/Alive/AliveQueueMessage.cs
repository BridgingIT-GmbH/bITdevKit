// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the built-in queueing alive probe enqueued from diagnostics surfaces.
/// </summary>
/// <example>
/// <code>
/// await broker.Enqueue(new AliveQueueMessage("dashboard"), cancellationToken);
/// </code>
/// </example>
public sealed class AliveQueueMessage : QueueMessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AliveQueueMessage"/> class.
    /// </summary>
    /// <param name="source">The component that requested the probe.</param>
    public AliveQueueMessage(string source = "dashboard")
    {
        this.Source = string.IsNullOrWhiteSpace(source) ? "dashboard" : source.Trim();
        this.CorrelationId = GuidGenerator.CreateSequential().ToString("N");
        this.Properties[Constants.CorrelationIdKey] = this.CorrelationId;
        this.Properties["Alive"] = true;
        this.Properties["Source"] = this.Source;
    }

    /// <summary>
    /// Gets the component that requested the probe.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the correlation identifier assigned to this probe.
    /// </summary>
    public string CorrelationId { get; }
}

