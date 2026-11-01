// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>An operation-owned copy of externally supplied HMAC key material.</summary>
/// <remarks>The host must generate at least 32 cryptographically random bytes and retain historical keys outside the audit store.</remarks>
/// <example><code>using var key = new AuditIntegrityKey("key-2026", protectedSecretBytes);</code></example>
[JsonConverter(typeof(AuditIntegrityKeyJsonConverter))]
public sealed class AuditIntegrityKey : IDisposable
{
    private byte[] material;

    /// <summary>Copies a bounded key and validates its safe non-secret identifier.</summary>
    /// <example><code>using var key = new AuditIntegrityKey(keyId, externalBytes);</code></example>
    public AuditIntegrityKey(string id, ReadOnlySpan<byte> material)
    {
        this.Id = ValidateId(id);
        if (material.Length is < 32 or > 4_096) throw new ArgumentException("Audit integrity key length is invalid.");
        this.material = material.ToArray();
    }

    /// <summary>The stable exact external key identity; never secret material.</summary>
    /// <example><code>var id = key.Id;</code></example>
    public string Id { get; }

    /// <summary>Read-only operation-owned key bytes, unavailable after disposal.</summary>
    /// <example><code>var tag = HMACSHA256.HashData(key.GetMaterial().Span, frame);</code></example>
    public ReadOnlyMemory<byte> GetMaterial() => this.material ?? throw new ObjectDisposedException(nameof(AuditIntegrityKey));

    /// <summary>Validates an identifier of at most 128 UTF-8 bytes without control characters.</summary>
    /// <example><code>var safeId = AuditIntegrityKey.ValidateId(keyId);</code></example>
    public static string ValidateId(string id)
    {
        var valid = AuditStructuralValidation.Required(id, 128, nameof(id));
        if (Encoding.UTF8.GetByteCount(valid) > 128) throw new ArgumentException("Audit integrity key identity is invalid.");
        return valid;
    }

    /// <summary>Clears this owned key copy; repeated disposal is safe.</summary>
    /// <example><code>key.Dispose();</code></example>
    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref this.material, null);
        if (owned is not null) CryptographicOperations.ZeroMemory(owned);
    }
}
