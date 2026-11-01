// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Configures optional whole-document HMAC protection using externally supplied host keys.</summary>
/// <example><code>services.AddAuditing().WithHmacSha256&lt;ExternalAuditKeys&gt;();</code></example>
public static class AuditingIntegrityBuilderExtensions
{
    /// <summary>Selects HMAC once for either built-in provider without creating or retaining a default key.</summary>
    /// <remarks>Keys are resolved in fresh operation scopes; writer queues contain only sealed documents.</remarks>
    /// <example><code>builder.WithHmacSha256&lt;ExternalAuditKeys&gt;(new() { AllowUnsignedHistory = false });</code></example>
    public static AuditingBuilder WithHmacSha256<TKeys>(this AuditingBuilder builder, AuditIntegrityOptions options = null)
        where TKeys : class, IAuditIntegrityKeyProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        options ??= new();
        options.Validate();
        if (builder.Services.Any(d => d.ServiceType == typeof(HmacSha256AuditIntegrityService)))
        {
            if (builder.Services.Any(d => d.ServiceType == typeof(IAuditIntegrityKeyProvider) && d.ImplementationType == typeof(TKeys))) return builder;
            throw new InvalidOperationException("An audit integrity key provider is already selected.");
        }

        if (builder.Services.Any(d => d.ServiceType == typeof(IAuditIntegrityVerifier))) throw new InvalidOperationException("An audit integrity verifier is already selected.");
        builder.Services.RemoveAll<IAuditIntegrityKeyProvider>();
        builder.Services.AddScoped<IAuditIntegrityKeyProvider, TKeys>();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<HmacSha256AuditIntegrityService>();
        builder.Services.AddSingleton<IAuditRecordSealer>(sp => sp.GetRequiredService<HmacSha256AuditIntegrityService>());
        builder.Services.AddSingleton<IAuditIntegrityVerifier>(sp => sp.GetRequiredService<HmacSha256AuditIntegrityService>());
        return builder;
    }
}
