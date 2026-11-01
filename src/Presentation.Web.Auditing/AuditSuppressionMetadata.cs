// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

/// <summary>Suppresses audit review routes and their nested operations independently of configurable path exclusions.</summary>
/// <example><code>endpoint.WithMetadata(new AuditSuppressionMetadata());</code></example>
public sealed record AuditSuppressionMetadata;
