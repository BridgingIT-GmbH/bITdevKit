// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Reports that publication was attempted while Broadcasting is disabled.</summary>
/// <example><code>result.Errors.ShouldContain(error => error is BroadcastingDisabledError);</code></example>
public sealed class BroadcastingDisabledError() : ResultErrorBase("Broadcasting is disabled.");

/// <summary>Reports an invalid broadcast publication request.</summary>
/// <param name="message">A safe description of the validation failure.</param>
/// <example><code>var error = new BroadcastValidationError("A scope is required.");</code></example>
public sealed class BroadcastValidationError(string message) : ResultErrorBase(message);

/// <summary>Reports an unavailable registry operation.</summary>
/// <param name="message">A safe description of the registry failure.</param>
/// <example><code>var error = new BroadcastRegistryUnavailableError("The registry is unavailable.");</code></example>
public sealed class BroadcastRegistryUnavailableError(string message) : ResultErrorBase(message);

/// <summary>Reports that the publishing node is not active in a shared registry.</summary>
/// <param name="message">A safe description of the missing sender registration.</param>
/// <example><code>var error = new BroadcastSenderNotRegisteredError("The sender is inactive.");</code></example>
public sealed class BroadcastSenderNotRegisteredError(string message) : ResultErrorBase(message);

/// <summary>Reports a target scope outside the sender's active registration.</summary>
/// <param name="message">A safe description of the forbidden scope.</param>
/// <example><code>var error = new BroadcastScopeForbiddenError("The scope is not registered.");</code></example>
public sealed class BroadcastScopeForbiddenError(string message) : ResultErrorBase(message);

/// <summary>Reports that a typed payload could not be serialized safely.</summary>
/// <param name="message">A safe description of the serialization failure.</param>
/// <example><code>var error = new BroadcastSerializationError("Serialization failed.");</code></example>
public sealed class BroadcastSerializationError(string message) : ResultErrorBase(message);

/// <summary>Reports that a publication required at least one target but found none.</summary>
/// <example><code>var error = new BroadcastNoTargetError();</code></example>
public sealed class BroadcastNoTargetError()
    : ResultErrorBase("No active broadcast targets were found.");

/// <summary>Reports a denied privileged Broadcasting operation.</summary>
/// <example><code>var error = new BroadcastOperationalAuthorizationError();</code></example>
public sealed class BroadcastOperationalAuthorizationError()
    : ResultErrorBase("The Broadcasting operational action is not authorized.");
