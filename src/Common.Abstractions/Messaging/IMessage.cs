// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation.Results;

/// <summary>
/// Represents a publishable outbound message contract shared across features.
/// </summary>
public interface IMessage
{
    /// <summary>Gets the identifier used to correlate and distinguish the message.</summary>
    string MessageId { get; }

    /// <summary>Gets the time associated with creation of the message.</summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>Gets extensible metadata that accompanies the message.</summary>
    IDictionary<string, object> Properties { get; }

    /// <summary>Validates the message before it is published or handled.</summary>
    /// <returns>A result containing any validation failures.</returns>
    ValidationResult Validate();
}
