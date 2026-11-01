// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Formats technical names for compact audit tables while preserving their terminal names.</summary>
/// <example><code>var display = AuditDashboardText.ShortenKey("Company.Application.Orders.CreateOrder.Handle");</code></example>
public static class AuditDashboardText
{
    /// <summary>Abbreviates namespaces and preserves the type/method suffix within an 80-character budget.</summary>
    /// <example><code>var display = AuditDashboardText.ShortenKey(record.Key);</code></example>
    public static string ShortenKey(string value)
    {
        const int MAXIMUM_LENGTH = 80;
        if (string.IsNullOrWhiteSpace(value)) return "-";
        if (value.Length <= MAXIMUM_LENGTH) return value;
        var separator = value.LastIndexOf('.');
        if (separator <= 0 || value.Length - separator >= MAXIMUM_LENGTH)
            return Shortener.Apply(value, MAXIMUM_LENGTH, ".");
        var method = value[separator..];
        return Shortener.Apply(value[..separator], MAXIMUM_LENGTH - method.Length, ".") + method;
    }

    /// <summary>Abbreviates namespaces using the same 46-character budget as Messaging type names.</summary>
    /// <example><code>var display = AuditDashboardText.ShortenType("Company.Application.Orders.CreateOrder");</code></example>
    public static string ShortenType(string value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : Shortener.Apply(value, 46, ".");
}
