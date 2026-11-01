// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;

/// <summary>Host identity and explicit outcome severity mapping for cef-v1. No transport is installed.</summary>
/// <example><code>var options = new CefAuditExportOptions { Vendor = "Northwind", Product = "CustomerPortal", Version = "1" };</code></example>
public sealed record CefAuditExportOptions
{
    /// <summary>Required producing organization.</summary>
    /// <example><code>var value = options.Vendor;</code></example>
    public string Vendor { get; init; }

    /// <summary>Required producing application.</summary>
    /// <example><code>var value = options.Product;</code></example>
    public string Product { get; init; }

    /// <summary>Required application version.</summary>
    /// <example><code>var value = options.Version;</code></example>
    public string Version { get; init; }

    /// <summary>Explicit 0..10 host severity values. Unmapped outcomes have severity zero.</summary>
    /// <example><code>var configured = options with { Severities = options.Severities.Add("Failed", 3) };</code></example>
    public ImmutableDictionary<string, int> Severities { get; init; } = ImmutableDictionary<string, int>.Empty;

    /// <summary>Rejects missing, excessive or invalid host configuration.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (new[] { this.Vendor, this.Product, this.Version }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1024)
            || this.Severities is null || this.Severities.Count > 16 || this.Severities.Any(pair => pair.Key.Length > 32 || pair.Value is < 0 or > 10))
            throw new ArgumentException("CEF requires bounded host identity and explicit 0..10 severity mappings.");
    }
}
