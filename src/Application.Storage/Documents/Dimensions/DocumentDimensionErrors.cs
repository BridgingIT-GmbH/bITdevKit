// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Reports an explicit Dimensions operation on an unsupported provider.</summary>
/// <example><code>var unsupported = result.Errors.OfType&lt;DocumentDimensionsNotSupportedError&gt;().Any();</code></example>
public sealed class DocumentDimensionsNotSupportedError() : ResultErrorBase("Provider does not support the requested Dimensions operation.");

/// <summary>Reports a verified digest collision without exposing dimension values.</summary>
/// <param name="collisionKind">The scope or name identity that collided.</param>
/// <example><code>var collision = result.Errors.OfType&lt;DocumentDimensionHashCollisionError&gt;().FirstOrDefault();</code></example>
public sealed class DocumentDimensionHashCollisionError(string collisionKind) : ResultErrorBase("Dimension dictionary hash collision.")
{
    /// <summary>Gets the collision kind.</summary>
    public string CollisionKind { get; } = collisionKind;
}

/// <summary>Reports exhaustion of the bounded dictionary registration race budget.</summary>
/// <remarks>The owner transaction was rolled back. Automatic retries must not multiply the registration budget.</remarks>
/// <example><code>var exhausted = result.Errors.OfType&lt;DocumentDimensionRegistrationContentionError&gt;().Any();</code></example>
public sealed class DocumentDimensionRegistrationContentionError() : ResultErrorBase("Dimension name registration contention exceeded its retry budget.");
