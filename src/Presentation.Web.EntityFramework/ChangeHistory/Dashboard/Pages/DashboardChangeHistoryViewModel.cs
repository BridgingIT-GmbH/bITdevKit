// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard.Pages;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.AspNetCore.Http;

/// <summary>
/// View model for the server-rendered ChangeHistory dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardChangeHistoryViewModel();
/// </code>
/// </example>
public sealed class DashboardChangeHistoryViewModel
{
    /// <summary>
    /// Gets or sets the UTC timestamp when this model was captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the dashboard page path.
    /// </summary>
    public string PagePath { get; set; } = "/_bdk/dashboard/change-history";

    /// <summary>
    /// Gets or sets the restore action path.
    /// </summary>
    public string RestoreActionPath { get; set; } = "/_bdk/dashboard/change-history/actions/restore";

    /// <summary>
    /// Gets or sets the registered ChangeHistory endpoint descriptors.
    /// </summary>
    public IReadOnlyList<ChangeHistoryDashboardDescriptor> Registrations { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected ChangeHistory endpoint descriptor.
    /// </summary>
    public ChangeHistoryDashboardDescriptor SelectedRegistration { get; set; }

    /// <summary>
    /// Gets or sets the query state displayed by the dashboard filters.
    /// </summary>
    public ChangeHistoryDashboardQueryState Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the grouped change set query result.
    /// </summary>
    public ChangeHistoryFindAllChangeSetsResult ChangeSets { get; set; }

    /// <summary>
    /// Gets or sets the raw row query result.
    /// </summary>
    public ChangeHistoryFindAllResult Rows { get; set; }

    /// <summary>
    /// Gets or sets one exact change set result when a change set id is selected.
    /// </summary>
    public ChangeHistoryChangeSetRecord SelectedChangeSet { get; set; }

    /// <summary>
    /// Gets the errors that occurred while building this model.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the dashboard has at least one ChangeHistory registration.
    /// </summary>
    public bool IsAvailable => this.Registrations.Count > 0 && this.SelectedRegistration is not null;
}

/// <summary>
/// Represents ChangeHistory dashboard filter state.
/// </summary>
/// <example>
/// <code>
/// var query = ChangeHistoryDashboardQueryState.FromRequest(httpContext);
/// </code>
/// </example>
public sealed class ChangeHistoryDashboardQueryState
{
    /// <summary>
    /// Gets or sets the selected registration key.
    /// </summary>
    public string RegistrationKey { get; set; }

    /// <summary>
    /// Gets or sets the entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the property name filter.
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the change set id filter.
    /// </summary>
    public Guid? ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the bulk operation id filter.
    /// </summary>
    public Guid? BulkOperationId { get; set; }

    /// <summary>
    /// Gets or sets the changed-by user id filter.
    /// </summary>
    public string ChangedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the changed date lower bound.
    /// </summary>
    public DateTimeOffset? ChangedDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the changed date upper bound.
    /// </summary>
    public DateTimeOffset? ChangedDateTo { get; set; }

    /// <summary>
    /// Gets or sets the operation filter.
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the capture source filter.
    /// </summary>
    public string CaptureSource { get; set; }

    /// <summary>
    /// Gets or sets the capture strategy filter.
    /// </summary>
    public string CaptureStrategy { get; set; }

    /// <summary>
    /// Gets or sets the capture status filter.
    /// </summary>
    public string CaptureStatus { get; set; }

    /// <summary>
    /// Gets or sets the current page.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether results are ordered oldest first.
    /// </summary>
    public bool OrderAscending { get; set; }

    /// <summary>
    /// Creates filter state from the current HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The parsed query state.</returns>
    public static ChangeHistoryDashboardQueryState FromRequest(HttpContext context)
    {
        var query = context.Request.Query;

        return new ChangeHistoryDashboardQueryState
        {
            RegistrationKey = GetString(query, "registration"),
            EntityId = GetString(query, "entityId"),
            PropertyName = GetString(query, "propertyName"),
            ChangeSetId = GetGuid(query, "changeSetId"),
            BulkOperationId = GetGuid(query, "bulkOperationId"),
            ChangedByUserId = GetString(query, "changedByUserId"),
            ChangedDateFrom = GetDateTimeOffset(query, "changedDateFrom"),
            ChangedDateTo = GetDateTimeOffset(query, "changedDateTo"),
            Operation = GetString(query, "operation"),
            CaptureSource = GetString(query, "captureSource"),
            CaptureStrategy = GetString(query, "captureStrategy"),
            CaptureStatus = GetString(query, "captureStatus"),
            Page = Math.Max(1, GetInt(query, "page") ?? 1),
            PageSize = Math.Clamp(GetInt(query, "pageSize") ?? 20, 1, 100),
            OrderAscending = GetBool(query, "orderAscending") ?? false
        };
    }

    /// <summary>
    /// Creates the application ChangeHistory query contract from the dashboard filter state.
    /// </summary>
    /// <param name="descriptor">The selected ChangeHistory registration.</param>
    /// <returns>The ChangeHistory query contract.</returns>
    public ChangeHistoryFindAllQuery ToFindAllQuery(ChangeHistoryDashboardDescriptor descriptor)
    {
        return new ChangeHistoryFindAllQuery
        {
            EntityType = descriptor?.EntityTypeName,
            EntityId = this.EntityId,
            PropertyName = this.PropertyName,
            ChangeSetId = this.ChangeSetId,
            BulkOperationId = this.BulkOperationId,
            ChangedByUserId = this.ChangedByUserId,
            ChangedDateFrom = this.ChangedDateFrom,
            ChangedDateTo = this.ChangedDateTo,
            Operation = this.Operation,
            CaptureSource = this.CaptureSource,
            CaptureStrategy = this.CaptureStrategy,
            CaptureStatus = this.CaptureStatus,
            Page = this.Page,
            PageSize = this.PageSize,
            OrderAscending = this.OrderAscending,
            IncludeValues = descriptor?.Options.IncludeValues ?? true
        };
    }

    private static string GetString(IQueryCollection query, string key)
        => query.TryGetValue(key, out var value) ? value.ToString().Trim() : null;

    private static int? GetInt(IQueryCollection query, string key)
        => int.TryParse(GetString(query, key), out var value) ? value : null;

    private static bool? GetBool(IQueryCollection query, string key)
        => bool.TryParse(GetString(query, key), out var value) ? value : null;

    private static Guid? GetGuid(IQueryCollection query, string key)
        => Guid.TryParse(GetString(query, key), out var value) ? value : null;

    private static DateTimeOffset? GetDateTimeOffset(IQueryCollection query, string key)
        => DateTimeOffset.TryParse(GetString(query, key), out var value) ? value : null;
}
