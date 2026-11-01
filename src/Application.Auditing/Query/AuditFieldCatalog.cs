// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Allowlisted typed fields shared by query validation, memory evaluation and relational translation.</summary>
/// <example><code>var field = catalog.Find("Record", "Key");</code></example>
public sealed class AuditFieldCatalog
{
    private readonly Dictionary<string, AuditQueryField> fields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the built-in catalog plus explicitly registered searchable details.</summary>
    /// <example><code>var catalog = new AuditFieldCatalog([]);</code></example>
    public AuditFieldCatalog(IEnumerable<AuditDetailField> details = null)
    {
        this.Add("Record", "AuditId", "Guid", false);
        this.Add("Record", "OriginAuditId", "Guid", false);
        this.Add("Record", "CausationAuditId", "Guid", false);
        this.Add("Record", "CausationActionId", "Guid", false);
        this.Add("Record", "StartedUtc", "Timestamp", false);
        this.Add("Record", "CompletedUtc", "Timestamp", false);
        this.Add("Record", "SchemaVersion", "Integer", false);
        this.Add("Record", "TotalActionCount", "Integer", false);
        this.Add("Record", "OmittedActionCount", "Integer", false);
        this.Add("Record", "OmittedDetailCount", "Integer", false);
        this.Add("Record", "FailedActionCount", "Integer", false);
        this.Add("Record", "HasFailedActions", "Boolean", false);
        this.Add("Record", "Key", "String", true);
        this.Add("Record", "DisplayName", "String", true);
        this.Add("Record", "Kind", "String", true);
        this.Add("Record", "Reason", "String", false);
        this.Add("Record", "Application", "String", false);
        this.Add("Record", "Node", "String", false);
        this.Add("Record", "ModuleName", "String", true);
        this.Add("Record", "InitiatingModuleName", "String", true);
        this.Add("Record", "CorrelationId", "String", false);
        this.Add("Record", "FlowId", "String", false);
        this.Add("Record", "JobName", "String", true);
        this.Add("Record", "RequestId", "String", false);
        this.Add("Record", "JobOccurrenceId", "String", false);
        this.Add("Record", "JobExecutionId", "String", false);
        this.Add("Record", "Outcome", "String", true);
        this.Add("Record", "Completeness", "String", true);
        this.Add("Record", "StorageState", "String", false);
        this.Add("Action", "Id", "Guid", false);
        this.Add("Action", "ParentActionId", "Guid", false);
        this.Add("Action", "StartedUtc", "Timestamp", false);
        this.Add("Action", "CompletedUtc", "Timestamp", false);
        this.Add("Action", "Sequence", "Integer", false);
        this.Add("Action", "Attempt", "Integer", false);
        this.Add("Action", "Key", "String", true);
        this.Add("Action", "DisplayName", "String", true);
        this.Add("Action", "Kind", "String", true);
        this.Add("Action", "Reason", "String", false);
        this.Add("Action", "Feature", "String", true);
        this.Add("Action", "ModuleName", "String", true);
        this.Add("Action", "CorrelationId", "String", false);
        this.Add("Action", "RequestType", "String", true);
        this.Add("Action", "HandlerType", "String", true);
        this.Add("Action", "ExecutionStatus", "String", true);
        this.Add("Action", "Outcome", "String", true);
        this.Add("Change", "Id", "Guid", false);
        this.Add("Change", "ActionId", "Guid", false);
        this.Add("Change", "Timestamp", "Timestamp", false);
        this.Add("Change", "SourceStoreKey", "String", false);
        this.Add("Change", "ChangeSetId", "String", false);
        this.Add("Change", "BulkOperationId", "String", false);
        this.Add("Change", "EntityType", "String", true);
        this.Add("Change", "EntityId", "String", false);
        this.Add("Change", "Operation", "String", true);
        this.Add("Change", "CaptureSource", "String", true);
        this.Add("Change", "PersistenceState", "String", true);
        this.Add("Resource", "Type", "String", true);
        this.Add("Resource", "Id", "String", false);
        this.Add("Resource", "DisplayName", "String", true);
        this.Add("Property", "Path", "String", false);
        this.Add("Property", "Operation", "String", true);
        this.Add("Property", "ItemIdentity", "String", false);
        this.Add("Record", "Actor.Kind", "String", true);
        this.Add("Record", "Actor.UserId", "String", false);
        this.Add("Record", "Actor.DisplayName", "String", true);
        this.Add("Record", "Actor.IsAuthenticated", "Boolean", false);
        this.Add("Record", "InitiatingActor.Kind", "String", true);
        this.Add("Record", "InitiatingActor.UserId", "String", false);
        this.Add("Record", "InitiatingActor.DisplayName", "String", true);
        this.Add("Record", "InitiatingActor.IsAuthenticated", "Boolean", false);
        this.Add("Record", "Http.Method", "String", true);
        this.Add("Record", "Http.Route", "String", false);
        this.Add("Record", "Http.Path", "String", false);
        this.Add("Record", "Http.Status", "Integer", false);
        this.Add("Record", "Http.Endpoint", "String", false);
        this.Add("Record", "Actions", "Collection", false, "Action");
        this.Add("Record", "EntityChanges", "Collection", false, "Change");
        this.Add("Record", "Resources", "Collection", false, "Resource");
        this.Add("Action", "Resources", "Collection", false, "Resource");
        this.Add("Change", "Properties", "Collection", false, "Property");
        this.Add("Record", "Id", "Guid", false, alias: "AuditId");
        this.Add("Record", "UserId", "String", false, alias: "Actor.UserId");
        this.Add("Record", "InitiatingUserId", "String", false, alias: "InitiatingActor.UserId");
        this.Add("Record", "UserName", "String", true, alias: "Actor.DisplayName");
        this.Add("Record", "InitiatingUserName", "String", true, alias: "InitiatingActor.DisplayName");
        foreach (var flag in new[] { "HasErrors", "HasCommittedChanges", "HasUnknownChanges", "HasTruncation" }) this.Add("Record", flag, "Boolean", false);
        this.Add("Record", "Http.StatusClass", "Integer", false);
        this.Add("Record", "Errors", "Collection", false, "Error");
        this.Add("Action", "Errors", "Collection", false, "Error");
        this.Add("Error", "Code", "String", true);
        this.Add("Error", "ExceptionType", "String", true);
        foreach (var detail in details ?? [])
        {
            _ = new AuditDetailName(detail.Name);
            if (detail.Kind is not ("String" or "Integer" or "Decimal" or "Boolean" or "Timestamp")) throw new ArgumentException("Unsupported audit detail type.");
            foreach (var domain in new[] { "Record", "Action" })
            {
                this.Add(domain, "Details." + detail.Name, detail.Kind, detail.IgnoreCase, detail: detail);
                this.Add(domain, "Details." + detail.Name + ".Marker", "String", true, detail: detail, marker: true);
            }
        }
    }

    /// <summary>Returns a known field or a payload-free validation failure.</summary>
    /// <example><code>var field = catalog.Find("Action", "Key");</code></example>
    public Result<AuditQueryField> Find(string domain, string name)
    {
        if (name is null || name.Length > 512 || (name.Equals("StorageState", StringComparison.OrdinalIgnoreCase) && name != "StorageState"))
            return Result<AuditQueryField>.Failure(new AuditError("AuditFieldUnsupported"));
        return this.fields.TryGetValue(domain + ":" + name, out var value) ? Result<AuditQueryField>.Success(value) : Result<AuditQueryField>.Failure(new AuditError("AuditFieldUnsupported"));
    }

    /// <summary>Returns safe catalog metadata without evaluating business callbacks.</summary>
    /// <example><code>var fields = catalog.Fields;</code></example>
    public IReadOnlyList<AuditQueryField> Fields => this.fields.Values.OrderBy(f => f.Domain, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal).ToArray();

    private void Add(string domain, string name, string kind, bool ignoreCase, string target = null, string alias = null, AuditDetailField detail = null, bool marker = false)
    {
        var field = new AuditQueryField { Domain = domain, Name = name, Kind = kind, IgnoreCase = ignoreCase, TargetDomain = target, Binding = alias ?? name, Detail = detail, IsMarker = marker };
        if (!this.fields.TryAdd(domain + ":" + name, field)) throw new ArgumentException("Duplicate audit field registration.");
    }
}

/// <summary>One immutable allowlisted query field; Binding is provider metadata, never client SQL.</summary>
/// <example><code>var name = field.Name;</code></example>
public sealed record AuditQueryField
{
    /// <summary>Field owner domain.</summary>
    /// <example><code>var value = field.Domain;</code></example>
    public string Domain { get; init; } = "Record";

    /// <summary>Public logical field name.</summary>
    /// <example><code>var value = field.Name;</code></example>
    public string Name { get; init; } = null;

    /// <summary>Typed scalar or owned collection kind.</summary>
    /// <example><code>var value = field.Kind;</code></example>
    public string Kind { get; init; } = "String";

    /// <summary>Whether string values use ordinal case-insensitive matching.</summary>
    /// <example><code>var value = field.IgnoreCase;</code></example>
    public bool IgnoreCase { get; init; } = false;

    /// <summary>Collection element domain when applicable.</summary>
    /// <example><code>var value = field.TargetDomain;</code></example>
    public string TargetDomain { get; init; } = null;

    /// <summary>Approved original-model binding name.</summary>
    /// <example><code>var value = field.Binding;</code></example>
    public string Binding { get; init; } = null;

    /// <summary>Explicit searchable-detail registration, if applicable.</summary>
    /// <example><code>var value = field.Detail;</code></example>
    public AuditDetailField Detail { get; init; } = null;

    /// <summary>Whether this field selects a policy marker instead of business data.</summary>
    /// <example><code>var value = field.IsMarker;</code></example>
    public bool IsMarker { get; init; } = false;
}

/// <summary>An explicit searchable-detail declaration with typed operators and exposure policy.</summary>
/// <example><code>var detail = new AuditDetailField { Name = "tenantId", Kind = "String" };</code></example>
public sealed record AuditDetailField
{
    /// <summary>The approved detail key.</summary>
    /// <example><code>var name = detail.Name;</code></example>
    public string Name { get; init; }

    /// <summary>The registered scalar type.</summary>
    /// <example><code>var type = detail.Kind;</code></example>
    public string Kind { get; init; } = "String";

    /// <summary>Whether string values use ordinal case-insensitive comparison. IDs should remain exact.</summary>
    /// <example><code>var exact = !detail.IgnoreCase;</code></example>
    public bool IgnoreCase { get; init; }

    /// <summary>Explicit allowed operators; an empty set uses the standard operators for the selected type.</summary>
    /// <example><code>var operators = detail.Operators;</code></example>
    public ImmutableHashSet<FilterOperator> Operators { get; init; } = ImmutableHashSet<FilterOperator>.Empty;

    /// <summary>Whether current response projections may expose the captured value.</summary>
    /// <example><code>var visible = detail.ExposeValue;</code></example>
    public bool ExposeValue { get; init; } = true;
}
