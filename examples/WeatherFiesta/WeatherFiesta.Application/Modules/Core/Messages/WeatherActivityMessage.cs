// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents a durable broker message describing a weather activity in the WeatherFiesta example.
/// </summary>
/// <remarks>
/// This message is published from the application's domain-event handlers so the Entity Framework broker
/// and operational messaging endpoints have real persisted traffic to inspect.
/// </remarks>
/// <example>
/// <code>
/// await broker.Publish(
///     new WeatherActivityMessage(cityId, "WeatherIngested", "Current weather data refreshed"),
///     cancellationToken);
/// </code>
/// </example>
public class WeatherActivityMessage : MessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherActivityMessage"/> class.
    /// </summary>
    public WeatherActivityMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherActivityMessage"/> class.
    /// </summary>
    /// <param name="cityId">The city identifier.</param>
    /// <param name="activityType">The activity type, such as WeatherIngested or ForecastUpdated.</param>
    /// <param name="details">Human-readable details about the activity.</param>
    public WeatherActivityMessage(string cityId, string activityType, string details)
    {
        this.CityId = cityId;
        this.ActivityType = activityType;
        this.Details = details;
        this.Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or sets the city identifier.
    /// </summary>
    public string CityId { get; set; }

    /// <summary>
    /// Gets or sets the activity type.
    /// </summary>
    public string ActivityType { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the activity message was created.
    /// </summary>
    public new DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets human-readable details about the activity.
    /// </summary>
    public string Details { get; set; }
}
