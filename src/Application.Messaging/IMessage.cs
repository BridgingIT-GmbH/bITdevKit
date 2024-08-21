// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using FluentValidation.Results;

public interface IMessage
{
    string MessageId { get; } // TODO: change to GUID like DomainEvent

    DateTimeOffset Timestamp { get; }

    IDictionary<string, object> Properties { get; }

    ValidationResult Validate();
}