﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using FluentValidation.Results;

/// <summary>
/// Initializes a new instance of the <see cref="MessageBase"/> class.
/// </summary>
public abstract class MessageBase(string id) : IMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBase"/> class.
    /// </summary>
    protected MessageBase()
        : this(GuidGenerator.CreateSequential().ToString("N"))
    {
    }

    /// <summary>
    /// Gets the id of this message.
    /// </summary>
    /// <value>
    /// The message identifier.
    /// </value>
    public string Id { get; set; } = id;

    /// <summary>
    /// Gets the timestamp when this message was created.
    /// </summary>
    /// <value>
    /// The message identifier.
    /// </value>
    public DateTimeOffset Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hold extra properties for this message.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Validates this message.
    /// </summary>
    public virtual ValidationResult Validate() => new();
}
