// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Broker message published by the WeatherFiesta hello-world orchestration sample.
/// </summary>
/// <example>
/// <code>
/// var message = new WeatherHelloWorldMessage("Hello WeatherFiesta", "manual", DateTimeOffset.UtcNow, ["Prepared"]);
/// </code>
/// </example>
public sealed class WeatherHelloWorldMessage : MessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherHelloWorldMessage" /> class.
    /// </summary>
    public WeatherHelloWorldMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherHelloWorldMessage" /> class.
    /// </summary>
    /// <param name="greeting">The greeting processed by the orchestration.</param>
    /// <param name="source">The source that started the orchestration.</param>
    /// <param name="requestedUtc">The UTC timestamp when the orchestration was requested.</param>
    /// <param name="steps">The orchestration steps captured before publishing.</param>
    public WeatherHelloWorldMessage(
        string greeting,
        string source,
        DateTimeOffset requestedUtc,
        IEnumerable<string> steps)
    {
        this.Scope = "hello-world";
        this.Greeting = greeting;
        this.Source = source;
        this.RequestedUtc = requestedUtc;
        this.Steps = steps?.ToList() ?? [];
    }

    /// <summary>
    /// Gets or sets the sample scope for dashboard filtering and inspection.
    /// </summary>
    public string Scope { get; set; } = "hello-world";

    /// <summary>
    /// Gets or sets the greeting processed by the orchestration.
    /// </summary>
    public string Greeting { get; set; }

    /// <summary>
    /// Gets or sets the source that started the orchestration.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the orchestration was requested.
    /// </summary>
    public DateTimeOffset RequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the orchestration steps captured before publishing.
    /// </summary>
    public List<string> Steps { get; set; } = [];
}
