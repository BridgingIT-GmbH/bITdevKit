// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard.Pages;

/// <summary>The filter state echoed back to the page: raw query values plus the validated conditions.</summary>
/// <param name="Query">The parsed query-string state.</param>
/// <param name="Conditions">The validated filter conditions (empty when none or invalid).</param>
internal sealed record EntitiesDashboardFilterState(
    EntitiesDashboardQuery Query,
    IReadOnlyList<EntitiesDashboardCondition> Conditions)
{
    /// <summary>Gets the match mode token, <c>and</c> or <c>or</c>.</summary>
    public string Match => this.Query.MatchAny ? "or" : "and";

    /// <summary>Gets the sort direction token, <c>asc</c> or <c>desc</c>.</summary>
    public string Direction => this.Query.Descending ? "desc" : "asc";
}

/// <summary>The model of the rows and load-more fragments (<c>Rows</c> slice): one batch of masked rows.</summary>
public sealed class EntitiesDashboardRowsViewModel
{
    /// <summary>Gets the base path of the Entities page (for building links).</summary>
    public string Path { get; init; }

    /// <summary>Gets the entity.</summary>
    internal EntitiesDashboardEntity Entity { get; init; }

    /// <summary>Gets the visible columns (id first; hidden and unsupported properties are already removed).</summary>
    internal IReadOnlyList<EntityPropertyDescriptor> Columns { get; init; } = [];

    /// <summary>Gets the projected rows keyed by property name; masked values are replaced by the mask text.</summary>
    public IReadOnlyList<IDictionary<string, object>> Rows { get; init; } = [];

    /// <summary>Gets the 1-based number of the loaded batch.</summary>
    public int Batch { get; init; } = 1;

    /// <summary>Gets the number of the next batch to request.</summary>
    public int NextBatch => this.Batch + 1;

    /// <summary>Gets the configured batch size.</summary>
    public int BatchSize { get; init; }

    /// <summary>Gets the selectable rows-per-load values.</summary>
    public IReadOnlyList<int> PageSizes { get; init; } = [];

    /// <summary>Gets a value indicating whether another batch may exist.</summary>
    public bool HasMore { get; init; }

    /// <summary>Gets the soft-deleted flag of each row (same order as <see cref="Rows"/>), or null when unknown.</summary>
    public IReadOnlyList<bool> DeletedFlags { get; init; }

    /// <summary>Gets a value indicating whether the user may edit (shows selection and edit actions).</summary>
    public bool CanEdit { get; init; }

    /// <summary>Gets the filter state echo.</summary>
    internal EntitiesDashboardFilterState Filter { get; init; }

    /// <summary>Gets the user-facing error, or null.</summary>
    public string Error { get; init; }

    /// <summary>Gets the correlation id of a server failure, or null.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets the HTTP status code of the load.</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>Gets the id text of a row.</summary>
    /// <param name="row">The projected row.</param>
    /// <returns>The id text, or an empty string.</returns>
    internal string GetId(IDictionary<string, object> row) =>
        this.Entity?.IdProperty != null && row.TryGetValue(this.Entity.IdProperty.Name, out var id) ? id?.ToString() : string.Empty;
}

/// <summary>The model of the detail fragment (<c>Detail</c> slice): one masked record.</summary>
public sealed class EntitiesDashboardDetailViewModel
{
    /// <summary>Gets the base path of the Entities page.</summary>
    public string Path { get; init; }

    /// <summary>Gets the entity.</summary>
    internal EntitiesDashboardEntity Entity { get; init; }

    /// <summary>Gets the source name.</summary>
    public string Source { get; init; }

    /// <summary>Gets the visible properties.</summary>
    internal IReadOnlyList<EntityPropertyDescriptor> Properties { get; init; } = [];

    /// <summary>Gets the projected masked values keyed by property name, or null on error.</summary>
    public IDictionary<string, object> Row { get; init; }

    /// <summary>Gets the id text.</summary>
    public string Id { get; init; }

    /// <summary>Gets the stored concurrency version text, or null.</summary>
    public string ConcurrencyVersion { get; init; }

    /// <summary>Gets a value indicating whether the record is soft-deleted (read-only).</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Gets the change-history link, or null when no provider supplies one.</summary>
    public string ChangeHistoryUrl { get; init; }

    /// <summary>Gets a value indicating whether the user may edit.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Gets a value indicating whether the entity supports updating.</summary>
    public bool CanUpdate => this.CanEdit && !this.IsDeleted && this.Entity?.CanUpdate == true;

    /// <summary>Gets a value indicating whether the entity supports deleting.</summary>
    public bool CanDelete => this.CanEdit && !this.IsDeleted && this.Entity?.CanDelete == true;

    /// <summary>Gets the user-facing error, or null.</summary>
    public string Error { get; init; }

    /// <summary>Gets the correlation id of a server failure, or null.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets the HTTP status code of the load.</summary>
    public int StatusCode { get; init; } = 200;
}

/// <summary>One field of the editor.</summary>
/// <param name="Property">The property descriptor.</param>
/// <param name="Value">The current masked display value (null for create, or when the value is null).</param>
/// <param name="Options">The selectable options for enum and enumeration properties, otherwise empty.</param>
/// <param name="Editable">True when the user may change the value (writable, not read-only, supported).</param>
internal sealed record EntitiesDashboardEditorField(
    EntityPropertyDescriptor Property,
    object Value,
    IReadOnlyList<EntitiesDashboardOption> Options,
    bool Editable);

/// <summary>The model of the editor fragment (<c>Editor</c> slice) for create and edit.</summary>
public sealed class EntitiesDashboardEditorViewModel
{
    /// <summary>Gets the base path of the Entities page.</summary>
    public string Path { get; init; }

    /// <summary>Gets the entity.</summary>
    internal EntitiesDashboardEntity Entity { get; init; }

    /// <summary>Gets the source name.</summary>
    public string Source { get; init; }

    /// <summary>Gets the mode, <c>create</c> or <c>edit</c>.</summary>
    public string Mode { get; init; } = "edit";

    /// <summary>Gets the id text (edit only).</summary>
    public string Id { get; init; }

    /// <summary>Gets the stored concurrency version text to post back (edit only), or null.</summary>
    public string ConcurrencyVersion { get; init; }

    /// <summary>Gets the fields in display order.</summary>
    internal IReadOnlyList<EntitiesDashboardEditorField> Fields { get; init; } = [];

    /// <summary>Gets a value indicating whether the user may edit.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Gets the capability note shown when the entity cannot be edited, or null.</summary>
    public string CapabilityNote { get; init; }

    /// <summary>Gets the user-facing error, or null.</summary>
    public string Error { get; init; }

    /// <summary>Gets the correlation id of a server failure, or null.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets the HTTP status code of the load.</summary>
    public int StatusCode { get; init; } = 200;
}

/// <summary>The model of the Entities page (<c>Index</c> slice): entity picker, filters and the first batch of rows.</summary>
public sealed class EntitiesDashboardViewModel
{
    /// <summary>Gets the base path of the Entities page.</summary>
    public string Path { get; init; }

    /// <summary>Gets the entities in the catalog, ordered by display name.</summary>
    internal IReadOnlyList<EntitiesDashboardEntity> Entities { get; init; } = [];

    /// <summary>Gets the selected entity, or null when none is selected.</summary>
    internal EntitiesDashboardEntity Entity { get; init; }

    /// <summary>Gets the selected source name.</summary>
    public string Source { get; init; }

    /// <summary>Gets the page mode: <c>list</c> (no entity or entity rows) or <c>detail</c> (an <c>id</c> is selected).</summary>
    public string Mode { get; init; } = "list";

    /// <summary>Gets a value indicating whether the user may edit.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Gets a value indicating whether edit is configured at all (otherwise the page shows a read-only notice).</summary>
    public bool IsEditConfigured { get; init; }

    /// <summary>Gets the filter state echo.</summary>
    internal EntitiesDashboardFilterState Filter { get; init; }

    /// <summary>Gets the filterable properties with their operator tokens.</summary>
    internal IReadOnlyList<EntityPropertyDescriptor> FilterableProperties { get; init; } = [];

    /// <summary>Gets the visible columns of the selected entity.</summary>
    internal IReadOnlyList<EntityPropertyDescriptor> Columns { get; init; } = [];

    /// <summary>Gets the first batch of rows, or null when no entity is selected.</summary>
    public EntitiesDashboardRowsViewModel Rows { get; init; }

    /// <summary>Gets the detail of the selected record, or null.</summary>
    public EntitiesDashboardDetailViewModel Detail { get; init; }

    /// <summary>Gets the configured batch size.</summary>
    public int BatchSize { get; init; }

    /// <summary>Gets the maximum number of rows selectable for a bulk delete.</summary>
    public int MaxBulkSelection { get; init; }

    /// <summary>Gets the maximum number of exported rows.</summary>
    public int MaxExportRows { get; init; }

    /// <summary>Gets the user-facing page error (for example an unknown entity), or null.</summary>
    public string Error { get; init; }
}
