// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the built-in messaging alive probe published from diagnostics surfaces.
/// </summary>
/// <example>
/// <code>
/// var message = new AliveMessage("dashboard");
/// await broker.Publish(message, cancellationToken);
/// </code>
/// </example>
public sealed class AliveMessage : MessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AliveMessage"/> class.
    /// </summary>
    /// <param name="source">The component that requested the probe.</param>
    public AliveMessage(string source = "dashboard")
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

