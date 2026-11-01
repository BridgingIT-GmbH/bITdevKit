// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Restricts verified original evidence using current capture and field-exposure rules.</summary>
/// <example><code>var response = projection.Apply(stored, "Verified");</code></example>
public sealed class AuditReadProjection(AuditingOptions options, AuditFieldCatalog catalog, AuditExposureOptions exposure)
{
    /// <summary>Produces an exposure-safe immutable response without changing or re-signing the source.</summary>
    /// <example><code>var details = projection.Apply(stored, "NotProtected");</code></example>
    public AuditRecordDetails Apply(AuditExtractionItem stored, string integrityStatus)
    {
        var original = stored.Record;
        var changed = false;
        AuditValue Project(string name, AuditValue value, bool allowed = true)
        {
            if (value is null) return null;
            var projected = AuditCapturePolicy.Project(name, value, options);
            if (!allowed && projected.Marker is null) projected = AuditValue.Policy("Redacted");
            else if (!allowed && projected.Marker == "Hashed") projected = AuditValue.Policy("Hashed");
            changed |= value.Marker != projected.Marker || value.Data.GetRawText() != projected.Data.GetRawText();
            return projected;
        }

        ImmutableDictionary<string, AuditValue> Details(ImmutableDictionary<string, AuditValue> details, string domain) => details.ToImmutableDictionary(
            pair => pair.Key, pair =>
            {
                var field = catalog.Find(domain, "Details." + pair.Key);
                return Project(pair.Key, pair.Value, field.IsFailure || field.Value.Detail.ExposeValue);
            }, StringComparer.OrdinalIgnoreCase);
        var record = original with
        {
            Details = Details(original.Details, "Record"),
            Actions = original.Actions.Select(action => action with { Details = Details(action.Details, "Action") }).ToImmutableArray(),
            EntityChanges = original.EntityChanges.Select(change => change with
            {
                Properties = change.Properties.Select(property => property with
                {
                    OldValue = Project(property.Path, property.OldValue, exposure.ExposeChangeValues && change.PersistenceState == "Committed"),
                    NewValue = Project(property.Path, property.NewValue, exposure.ExposeChangeValues && change.PersistenceState == "Committed")
                }).ToImmutableArray()
            }).ToImmutableArray()
        };
        // Responses contain only the current permitted model. Retained unknown source fields
        // remain available to the verifier and controlled test access, never implicit response encoding.
        record = record with { OriginalDocument = default, OriginalProjectionHash = null };
        changed |= AuditCanonicalizerV1.Hash(AuditRecordCodec.CanonicalDocument(record)) != original.ContentHash;
        return new AuditRecordDetails { Record = record, StorageState = stored.StorageState, IntegrityStatus = integrityStatus, IsTransformed = changed };
    }
}
