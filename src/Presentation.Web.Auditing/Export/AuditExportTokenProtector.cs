// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.DataProtection;

/// <summary>Protects bounded export progress for one current principal, separately from source HMAC keys.</summary>
/// <example><code>var protector = new AuditExportTokenProtector(dataProtection, options, TimeProvider.System);</code></example>
public sealed class AuditExportTokenProtector(IDataProtectionProvider protection, AuditExportOptions options, TimeProvider time)
{
    private readonly IDataProtector protector = protection.CreateProtector("BridgingIT.DevKit.Auditing.ExportProgress.v1");

    /// <summary>Hashes bounded current claims. Claims are never retained in descriptors or tokens.</summary>
    /// <example><code>var binding = AuditExportTokenProtector.PrincipalFingerprint(http.User);</code></example>
    public static string PrincipalFingerprint(ClaimsPrincipal principal)
    {
        var identities = principal?.Identities.Take(17).ToArray() ?? [];
        if (identities.Length > 16) throw new AuditExportException("AuditExportPrincipalTooLarge");
        var count = 0; var characters = 0;
        string Bounded(string value, int maximum)
        {
            if (value?.Length > maximum || (characters = checked(characters + (value?.Length ?? 0))) > 8192) throw new AuditExportException("AuditExportPrincipalTooLarge");
            return value;
        }

        var values = identities.Select(identity => new
        {
            AuthenticationType = Bounded(identity.AuthenticationType, 512), NameClaimType = Bounded(identity.NameClaimType, 1024), RoleClaimType = Bounded(identity.RoleClaimType, 1024),
            identity.IsAuthenticated,
            Claims = identity.Claims.Take(129).Select(claim =>
            {
                if (++count > 128) throw new AuditExportException("AuditExportPrincipalTooLarge");
                return new { Type = Bounded(claim.Type, 1024), Value = Bounded(claim.Value, 4096), Issuer = Bounded(claim.Issuer, 1024) };
            }).OrderBy(claim => claim.Type, StringComparer.Ordinal).ThenBy(claim => claim.Value, StringComparer.Ordinal).ThenBy(claim => claim.Issuer, StringComparer.Ordinal).ToArray()
        }).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(values);
        try { if (bytes.Length > 32_768) throw new AuditExportException("AuditExportPrincipalTooLarge"); return Convert.ToHexStringLower(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Issues protected progress only after output confirmation.</summary>
    /// <example><code>var token = protector.Protect(completion, principalBinding);</code></example>
    public string Protect(AuditExportCompletion completion, string principalBinding)
    {
        if (!completion.OutputConfirmed || completion.Progress is null || principalBinding?.Length != 64) throw new AuditExportException("AuditExportOutputUnconfirmed");
        var token = this.protector.Protect(JsonSerializer.Serialize(new Envelope(principalBinding, completion.Progress), AuditExportJson.CreateOptions()));
        if (token.Length > options.TokenCharacterLimit) throw new AuditExportException("AuditExportTokenTooLarge");
        return token;
    }

    /// <summary>Rejects tampering, expiry, oversized input and changed principal before opening a source window.</summary>
    /// <example><code>var resume = protector.Unprotect(token, principalBinding);</code></example>
    public Result<AuditExportResume> Unprotect(string token, string principalBinding)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > options.TokenCharacterLimit) return Invalid();
            var json = this.protector.Unprotect(token);
            if (json.Length > 8192) return Invalid();
            var envelope = JsonSerializer.Deserialize<Envelope>(json, AuditExportJson.CreateOptions());
            return envelope?.Principal != principalBinding || envelope.Progress is null || envelope.Progress.ExpiresUtc <= time.GetUtcNow() ? Invalid() : Result<AuditExportResume>.Success(envelope.Progress);
        }
        catch (Exception) { return Invalid(); }
    }

    private static Result<AuditExportResume> Invalid() => Result<AuditExportResume>.Failure(new AuditError("AuditExportProgressInvalid"));
    private sealed record Envelope(string Principal, AuditExportResume Progress);
}
