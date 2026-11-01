// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Reads approved scalar bindings from immutable audit models without reflection.</summary>
/// <example><code>var value = AuditFieldReader.Read(catalog.Find("Record", "Key").Value, record);</code></example>
public static class AuditFieldReader
{
    /// <summary>Reads a captured scalar or preserves its missing, null, or policy-marker state.</summary>
    /// <param name="field">An approved catalog field.</param>
    /// <param name="instance">The immutable model that owns the field.</param>
    /// <param name="storageState">Storage lifecycle metadata kept outside the captured record.</param>
    /// <returns>A copied scalar or an explicit non-comparable state.</returns>
    /// <example><code>var state = AuditFieldReader.Read(catalog.Find("Record", "StorageState").Value, record, "Archived");</code></example>
    public static AuditQueryValue Read(AuditQueryField field, object instance, string storageState = "Active")
    {
        if (field is null || instance is null || field.Kind == "Collection")
        {
            return State("Missing");
        }

        if (field.Detail is not null)
        {
            return (field.Domain, instance) switch
            {
                ("Record", AuditRecord record) => ReadDetail(field, record.Details),
                ("Action", AuditAction action) => ReadDetail(field, action.Details),
                _ => State("Missing")
            };
        }

        return (field.Domain, instance) switch
        {
            ("Record", AuditRecord record) => ReadRecord(field.Binding, record, storageState),
            ("Action", AuditAction action) => ReadAction(field.Binding, action),
            ("Change", AuditChangeEvidence change) => ReadChange(field.Binding, change),
            ("Resource", AuditResourceReference resource) => ReadResource(field.Binding, resource),
            ("Error", AuditSafeError error) => field.Binding switch { "Code" => Scalar(error.Code, "String"), "ExceptionType" => Scalar(error.ExceptionType, "String"), _ => State("Missing") },
            ("Property", AuditPropertyChange property) => ReadProperty(field.Binding, property),
            _ => State("Missing")
        };
    }

    private static AuditQueryValue ReadRecord(string binding, AuditRecord record, string storageState) => binding switch
    {
        "AuditId" or "Id" => Scalar(record.AuditId, "Guid"),
        "OriginAuditId" => Scalar(record.OriginAuditId, "Guid"),
        "CausationAuditId" => Scalar(record.CausationAuditId, "Guid"),
        "CausationActionId" => Scalar(record.CausationActionId, "Guid"),
        "StartedUtc" => Scalar(record.StartedUtc, "Timestamp"),
        "CompletedUtc" => Scalar(record.CompletedUtc, "Timestamp"),
        "SchemaVersion" => Scalar(record.SchemaVersion, "Integer"),
        "TotalActionCount" => Scalar(record.TotalActionCount, "Integer"),
        "OmittedActionCount" => Scalar(record.OmittedActionCount, "Integer"),
        "OmittedDetailCount" => Scalar(record.OmittedDetailCount, "Integer"),
        "FailedActionCount" => Scalar(record.FailedActionCount, "Integer"),
        "HasFailedActions" => Scalar(record.HasFailedActions, "Boolean"),
        "Key" => Scalar(record.Key, "String"),
        "DisplayName" => Scalar(record.DisplayName, "String"),
        "Kind" => Scalar(record.Kind, "String"),
        "Reason" => Scalar(record.Reason, "String"),
        "Application" => Scalar(record.Application, "String"),
        "Node" => Scalar(record.Node, "String"),
        "ModuleName" => Scalar(record.ModuleName, "String"),
        "InitiatingModuleName" => Scalar(record.InitiatingModuleName, "String"),
        "CorrelationId" => Scalar(record.CorrelationId, "String"),
        "FlowId" => Scalar(record.FlowId, "String"),
        "JobName" => Scalar(record.JobName, "String"),
        "HasErrors" => Scalar(record.Errors.Length > 0 || record.Actions.Any(a => a.Errors.Length > 0), "Boolean"),
        "HasCommittedChanges" => Scalar(record.EntityChanges.Any(c => c.PersistenceState == "Committed"), "Boolean"),
        "HasUnknownChanges" => Scalar(record.EntityChanges.Any(c => c.PersistenceState == "Unknown"), "Boolean"),
        "HasTruncation" => Scalar(record.OmittedActionCount > 0 || record.OmittedDetailCount > 0 || record.Completeness == AuditCompleteness.Truncated ||
            record.Notes.Any(note => note.Code is not ("AuditRootIncomplete" or "AuditActionIncomplete" or "AuditActiveChildrenAtClosure" or "AuditOutOfOrderClosure" or "AuditCaptureExpired" or "AuditRuntimeStopped")), "Boolean"),
        "Http.StatusClass" => Scalar(record.Http?.Status / 100, "Integer"),
        "RequestId" => Scalar(record.RequestId, "String"),
        "JobOccurrenceId" => Scalar(record.JobOccurrenceId, "String"),
        "JobExecutionId" => Scalar(record.JobExecutionId, "String"),
        "Outcome" => Scalar(record.Outcome, "String"),
        "Completeness" => Scalar(record.Completeness, "String"),
        "StorageState" => Scalar(storageState, "String"),
        "Actor.Kind" => Scalar(record.Actor?.Kind, "String"),
        "Actor.UserId" => Scalar(record.Actor?.UserId, "String"),
        "Actor.DisplayName" => Scalar(record.Actor?.DisplayName, "String"),
        "Actor.IsAuthenticated" => Scalar(record.Actor?.IsAuthenticated, "Boolean"),
        "InitiatingActor.Kind" => Scalar(record.InitiatingActor?.Kind, "String"),
        "InitiatingActor.UserId" => Scalar(record.InitiatingActor?.UserId, "String"),
        "InitiatingActor.DisplayName" => Scalar(record.InitiatingActor?.DisplayName, "String"),
        "InitiatingActor.IsAuthenticated" => Scalar(record.InitiatingActor?.IsAuthenticated, "Boolean"),
        "Http.Method" => Scalar(record.Http?.Method, "String"),
        "Http.Route" => Scalar(record.Http?.Route, "String"),
        "Http.Path" => Scalar(record.Http?.Path, "String"),
        "Http.Status" => Scalar(record.Http?.Status, "Integer"),
        "Http.Endpoint" => Scalar(record.Http?.Endpoint, "String"),
        _ => State("Missing")
    };

    private static AuditQueryValue ReadAction(string binding, AuditAction action) => binding switch
    {
        "Id" => Scalar(action.Id, "Guid"),
        "ParentActionId" => Scalar(action.ParentActionId, "Guid"),
        "StartedUtc" => Scalar(action.StartedUtc, "Timestamp"),
        "CompletedUtc" => Scalar(action.CompletedUtc, "Timestamp"),
        "Sequence" => Scalar(action.Sequence, "Integer"),
        "Attempt" => Scalar(action.Attempt, "Integer"),
        "Key" => Scalar(action.Key, "String"),
        "DisplayName" => Scalar(action.DisplayName, "String"),
        "Kind" => Scalar(action.Kind, "String"),
        "Reason" => Scalar(action.Reason, "String"),
        "Feature" => Scalar(action.Feature, "String"),
        "ModuleName" => Scalar(action.ModuleName, "String"),
        "CorrelationId" => Scalar(action.CorrelationId, "String"),
        "RequestType" => Scalar(action.RequestType, "String"),
        "HandlerType" => Scalar(action.HandlerType, "String"),
        "ExecutionStatus" => Scalar(action.ExecutionStatus, "String"),
        "Outcome" => Scalar(action.Outcome, "String"),
        _ => State("Missing")
    };

    private static AuditQueryValue ReadChange(string binding, AuditChangeEvidence change) => binding switch
    {
        "Id" => Scalar(change.Id, "Guid"),
        "ActionId" => Scalar(change.ActionId, "Guid"),
        "Timestamp" => Scalar(change.Timestamp, "Timestamp"),
        "SourceStoreKey" => Scalar(change.SourceStoreKey, "String"),
        "ChangeSetId" => Scalar(change.ChangeSetId, "String"),
        "BulkOperationId" => Scalar(change.BulkOperationId, "String"),
        "EntityType" => Scalar(change.EntityType, "String"),
        "EntityId" => Scalar(change.EntityId, "String"),
        "Operation" => Scalar(change.Operation, "String"),
        "CaptureSource" => Scalar(change.CaptureSource, "String"),
        "PersistenceState" => Scalar(change.PersistenceState, "String"),
        _ => State("Missing")
    };

    private static AuditQueryValue ReadResource(string binding, AuditResourceReference resource) => binding switch
    {
        "Type" => Scalar(resource.Type, "String"),
        "Id" => Scalar(resource.Id, "String"),
        "DisplayName" => Scalar(resource.DisplayName, "String"),
        _ => State("Missing")
    };

    private static AuditQueryValue ReadProperty(string binding, AuditPropertyChange property) => binding switch
    {
        "Path" => Scalar(property.Path, "String"),
        "Operation" => Scalar(property.Operation, "String"),
        "ItemIdentity" => Scalar(property.ItemIdentity, "String"),
        _ => State("Missing")
    };

    private static AuditQueryValue ReadDetail(AuditQueryField field, ImmutableDictionary<string, AuditValue> details)
    {
        var expectedBinding = "Details." + field.Detail.Name + (field.IsMarker ? ".Marker" : string.Empty);
        if (!string.Equals(field.Binding, expectedBinding, StringComparison.OrdinalIgnoreCase))
        {
            return State("Missing");
        }

        AuditValue retained = null;
        var found = false;
        if (details is not null)
        {
            foreach (var entry in details)
            {
                if (string.Equals(entry.Key, field.Detail.Name, StringComparison.OrdinalIgnoreCase))
                {
                    retained = entry.Value;
                    found = true;
                    break;
                }
            }
        }

        if (field.IsMarker)
        {
            return Scalar(!found ? "Missing" : retained is null ? "Unavailable" : retained.Marker ?? "Value", "String");
        }

        if (!found)
        {
            return State("Missing");
        }

        if (retained is null)
        {
            return State("Unavailable");
        }

        return retained.Marker is null ? Scalar(retained.Data, field.Detail.Kind) : State(retained.Marker);
    }

    private static AuditQueryValue Scalar(object value, string kind)
    {
        var converted = AuditQueryValues.Convert(value, kind);
        return converted.IsSuccess ? converted.Value : State("Unavailable");
    }

    private static AuditQueryValue State(string kind) => new() { Kind = kind };
}
