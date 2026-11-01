// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Authenticates complete finalized originals with the pinned version-one structured HMAC frame.</summary>
/// <remarks>HMAC does not prove capture truth, completeness, privileged deletion prevention, or non-repudiation.</remarks>
/// <example><code>var protectedRecord = await integrity.SealAsync(frozenRecord);</code></example>
public sealed class HmacSha256AuditIntegrityService(IServiceScopeFactory scopes, AuditIntegrityOptions options, AuditingOptions capture) : IAuditRecordSealer, IAuditIntegrityVerifier
{
    /// <summary>Maximum reserved owned-envelope overhead within the configured complete-record bound.</summary>
    /// <example><code>var reserve = HmacSha256AuditIntegrityService.ENVELOPE_RESERVE_BYTES;</code></example>
    public const int ENVELOPE_RESERVE_BYTES = 1_024;

    /// <summary>Seals new frozen content once; already protected content must verify and remains unchanged.</summary>
    /// <example><code>var sealedRecord = await integrity.SealAsync(record, cancellationToken);</code></example>
    public async Task<Result<AuditRecord>> SealAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditRecord>.Failure(frozen);
        if (record.IntegrityProtection is not null)
        {
            var verified = await this.VerifyAsync(record, cancellationToken).ConfigureAwait(false);
            if (verified.IsFailure) return Result<AuditRecord>.Failure(verified);
            return verified.Value == "Verified" ? frozen : Result<AuditRecord>.Failure(new AuditError("AuditIntegrityInvalid"));
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            using var suppression = scope.ServiceProvider.GetRequiredService<IAuditContext>().Suppress();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.KeyResolutionTimeout);
            var resolved = await ResolveOwnedAsync(scope.ServiceProvider.GetRequiredService<IAuditIntegrityKeyProvider>().ResolveActiveAsync(timeout.Token), timeout.Token).ConfigureAwait(false);
            if (resolved.IsFailure || resolved.Value is null) return Result<AuditRecord>.Failure(new AuditError("AuditIntegrityUnavailable"));
            using var key = resolved.Value;
            var envelope = new AuditIntegrityProtection { KeyId = key.Id };
            var frame = CreateFrame(frozen.Value, envelope);
            var tag = ComputeAuthenticationTag(key.GetMaterial().Span, frame);
            var protectedRecord = frozen.Value with { IntegrityProtection = envelope with { Tag = Convert.ToBase64String(tag) } };
            if (JsonSerializer.SerializeToUtf8Bytes(protectedRecord.IntegrityProtection).Length > ENVELOPE_RESERVE_BYTES || AuditRecordCodec.Encode(protectedRecord).Length > capture.Limits.MaxRecordBytes)
                return Result<AuditRecord>.Failure(new AuditError("AuditRecordTooLarge"));
            return Result<AuditRecord>.Success(protectedRecord);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Result<AuditRecord>.Failure(new AuditError("AuditIntegrityUnavailable")); }
    }

    /// <summary>Verifies the original complete graph, strict full tag, authenticated header and external historical key.</summary>
    /// <example><code>var status = await integrity.VerifyAsync(original, cancellationToken);</code></example>
    public async Task<Result<string>> VerifyAsync(AuditRecord original, CancellationToken cancellationToken = default)
    {
        if (original is null) return Invalid();
        var protection = original.IntegrityProtection;
        if (protection is null) return options.AllowUnsignedHistory ? Result<string>.Success("NotProtected") : Result<string>.Failure(new AuditError("AuditIntegrityMissingProtection"));
        if (protection.ProtectionVersion != 1 || protection.CanonicalizationVersion != 1 || protection.Algorithm != "HMAC-SHA-256") return Result<string>.Failure(new AuditError("AuditIntegrityUnavailable"));
        try
        {
            _ = AuditIntegrityKey.ValidateId(protection.KeyId);
            var supplied = new byte[32];
            if (protection.Tag is null || protection.Tag.Length != 44 || !Convert.TryFromBase64String(protection.Tag, supplied, out var written) || written != 32 || Convert.ToBase64String(supplied) != protection.Tag) return Invalid();
            var frozen = AuditRecordCodec.Freeze(original);
            if (frozen.IsFailure) return Invalid();
            await using var scope = scopes.CreateAsyncScope();
            using var suppression = scope.ServiceProvider.GetRequiredService<IAuditContext>().Suppress();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.KeyResolutionTimeout);
            var resolved = await ResolveOwnedAsync(scope.ServiceProvider.GetRequiredService<IAuditIntegrityKeyProvider>().ResolveAsync(protection.KeyId, timeout.Token), timeout.Token).ConfigureAwait(false);
            if (resolved.IsFailure || resolved.Value is null) return Result<string>.Failure(new AuditError("AuditIntegrityUnavailable"));
            using var key = resolved.Value;
            if (key.Id != protection.KeyId) return Invalid();
            var expected = ComputeAuthenticationTag(key.GetMaterial().Span, CreateFrame(frozen.Value, protection));
            return CryptographicOperations.FixedTimeEquals(expected, supplied) ? Result<string>.Success("Verified") : Invalid();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ArgumentException) { return Invalid(); }
        catch (Exception) { return Result<string>.Failure(new AuditError("AuditIntegrityUnavailable")); }
    }

    /// <summary>Creates pinned canonical structured input, excluding only schema-defined root hash/envelope members.</summary>
    /// <example><code>var frame = HmacSha256AuditIntegrityService.CreateFrame(original, envelope);</code></example>
    public static byte[] CreateFrame(AuditRecord original, AuditIntegrityProtection protection)
    {
        using var document = JsonDocument.Parse(AuditRecordCodec.CanonicalDocument(original));
        return AuditCanonicalizerV1.Encode(JsonSerializer.SerializeToElement(new
        {
            purpose = "bdk.auditing.record", protectionVersion = protection.ProtectionVersion, algorithm = protection.Algorithm,
            keyId = protection.KeyId, canonicalizationVersion = protection.CanonicalizationVersion, document = document.RootElement
        }));
    }

    /// <summary>Computes a full SHA-256 HMAC with an external key of at least 256 bits.</summary>
    /// <example><code>var tag = HmacSha256AuditIntegrityService.ComputeAuthenticationTag(key.GetMaterial().Span, frame);</code></example>
    public static byte[] ComputeAuthenticationTag(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        if (key.Length is < 32 or > 4_096) throw new ArgumentException("Audit integrity key length is invalid.");
        return HMACSHA256.HashData(key, input);
    }

    private static async Task<Result<AuditIntegrityKey>> ResolveOwnedAsync(Task<Result<AuditIntegrityKey>> pending, CancellationToken token)
    {
        try { return await pending.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            _ = pending.ContinueWith(completed =>
            {
                if (completed.Status == TaskStatus.RanToCompletion && completed.Result.IsSuccess) completed.Result.Value?.Dispose();
                else _ = completed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }
    private static Result<string> Invalid() => Result<string>.Failure(new AuditError("AuditIntegrityInvalid"));
}
