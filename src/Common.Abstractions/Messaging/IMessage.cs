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
    string MessageId { get; }

    DateTimeOffset Timestamp { get; }

    IDictionary<string, object> Properties { get; }

    ValidationResult Validate();
}
