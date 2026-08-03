// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Protects opaque continuation-token payloads against tampering.
/// </summary>
/// <example>
/// <code>
/// var protector = new HmacContinuationTokenProtector(signingKey);
/// var protectedPayload = protector.Protect("blob-storage", payload);
/// </code>
/// </example>
public interface IContinuationTokenProtector
{
    /// <summary>
    /// Protects a payload for a specific feature purpose.
    /// </summary>
    /// <param name="purpose">The purpose binding.</param>
    /// <param name="payload">The unprotected payload.</param>
    /// <returns>The protected payload.</returns>
    /// <example>
    /// <code>
    /// var protectedPayload = protector.Protect("blob-storage", payload);
    /// </code>
    /// </example>
    byte[] Protect(string purpose, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Validates and unprotects a payload for a specific feature purpose.
    /// </summary>
    /// <param name="purpose">The purpose binding.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <param name="payload">The validated payload.</param>
    /// <returns>True when validation succeeds.</returns>
    /// <example>
    /// <code>
    /// if (protector.TryUnprotect("blob-storage", protectedPayload, out var payload)) { }
    /// </code>
    /// </example>
    bool TryUnprotect(string purpose, ReadOnlySpan<byte> protectedPayload, out byte[] payload);
}
