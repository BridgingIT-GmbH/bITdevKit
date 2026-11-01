// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Globalization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;

/// <summary>Shares bounded filters across audit list, related, history and statistics commands.</summary>
/// <example><code>var command = new AuditListConsoleCommand { CorrelationId = "customer-42", Take = 25 };</code></example>
public abstract class AuditQueryConsoleCommandBase(string name, string description) : AuditConsoleCommandBase(name, description)
{
    /// <summary>Gets or sets the exact root action key.</summary>
    /// <example><code>command.Key = "customers.rename";</code></example>
    [ConsoleCommandOption("key", Description = "Exact root action key")]
    public string Key { get; set; }

    /// <summary>Gets or sets a nested action key.</summary>
    /// <example><code>command.Action = "customers.save";</code></example>
    [ConsoleCommandOption("action", Description = "Exact nested action key")]
    public string Action { get; set; }

    /// <summary>Gets or sets the executing actor identifier.</summary>
    /// <example><code>command.Actor = "user-42";</code></example>
    [ConsoleCommandOption("actor", Description = "Exact executing actor ID")]
    public string Actor { get; set; }

    /// <summary>Gets or sets the correlation identifier.</summary>
    /// <example><code>command.CorrelationId = "customer-42";</code></example>
    [ConsoleCommandOption("correlation", Description = "Exact correlation ID")]
    public string CorrelationId { get; set; }

    /// <summary>Gets or sets the module name.</summary>
    /// <example><code>command.Module = "Customers";</code></example>
    [ConsoleCommandOption("module", Description = "Exact module name")]
    public string Module { get; set; }

    /// <summary>Gets or sets the root outcome filter.</summary>
    /// <example><code>command.Outcome = "Failed";</code></example>
    [ConsoleCommandOption("outcome", Description = "Root outcome, such as Failed or Rejected")]
    public string Outcome { get; set; }

    /// <summary>Gets or sets the inclusive start time.</summary>
    /// <example><code>command.From = "2026-10-31T00:00:00Z";</code></example>
    [ConsoleCommandOption("from", Description = "Inclusive ISO start time; UTC when offset is omitted")]
    public string From { get; set; }

    /// <summary>Gets or sets the exclusive end time.</summary>
    /// <example><code>command.To = "2026-11-01T00:00:00Z";</code></example>
    [ConsoleCommandOption("to", Description = "Exclusive ISO end time; UTC when offset is omitted")]
    public string To { get; set; }

    /// <summary>Gets or sets the selected active/archive dataset.</summary>
    /// <example><code>command.Lifecycle = "All";</code></example>
    [ConsoleCommandOption("lifecycle", Description = "Active, Archived, or All", Default = "Active")]
    public string Lifecycle { get; set; } = "Active";

    /// <summary>Gets or sets the one-based result page.</summary>
    /// <example><code>command.Page = 2;</code></example>
    [ConsoleCommandOption("page", Description = "One-based page number", Default = 1)]
    public int Page { get; set; } = 1;

    /// <summary>Gets or sets the page size, bounded to 200.</summary>
    /// <example><code>command.Take = 25;</code></example>
    [ConsoleCommandOption("take", Description = "Page size from 1 to 200", Default = 25)]
    public int Take { get; set; } = 25;

    /// <summary>Gets or sets whether retained child failures are required.</summary>
    /// <example><code>command.FailedActions = true;</code></example>
    [ConsoleCommandOption("failed-actions", Description = "Require failed child actions")]
    public bool FailedActions { get; set; }

    /// <summary>Gets or sets whether truncated evidence is required.</summary>
    /// <example><code>command.Truncated = true;</code></example>
    [ConsoleCommandOption("truncated", Description = "Require evidence truncation")]
    public bool Truncated { get; set; }

    /// <summary>Builds the existing FilterModel for validation and access scoping by the audit query service.</summary>
    protected FilterModel BuildFilter()
    {
        if (this.Page < 1 || this.Take is < 1 or > 200 || this.Lifecycle is not ("Active" or "Archived" or "All")) throw new ArgumentException("Invalid audit query bounds.");
        var filter = new FilterModel { Page = this.Page, PageSize = this.Take, Filters = [] };
        void Scalar(string field, string value)
        {
            if (value?.Length > 1024) throw new ArgumentException("Invalid audit filter.");
            if (!string.IsNullOrEmpty(value)) filter.Filters.Add(new(field, FilterOperator.Equal, value));
        }

        Scalar("Key", this.Key); Scalar("UserId", this.Actor); Scalar("CorrelationId", this.CorrelationId); Scalar("ModuleName", this.Module); Scalar("Outcome", this.Outcome);
        if (this.Action is { Length: > 0 })
        {
            if (this.Action.Length > 512) throw new ArgumentException("Invalid audit action key.");
            filter.Filters.Add(new("Actions", FilterOperator.Any, null) { Filters = [new("Key", FilterOperator.Equal, this.Action)] });
        }

        foreach (var (text, operation) in new[] { (this.From, FilterOperator.GreaterThanOrEqual), (this.To, FilterOperator.LessThan) })
        {
            if (string.IsNullOrEmpty(text)) continue;
            if (text.Length > 64 || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)) throw new ArgumentException("Invalid audit date.");
            filter.Filters.Add(new("StartedUtc", operation, timestamp));
        }

        filter.Filters.Add(new("StorageState", this.Lifecycle == "All" ? FilterOperator.IsNotNull : FilterOperator.Equal, this.Lifecycle == "All" ? null : this.Lifecycle));
        if (this.FailedActions) filter.Filters.Add(new("HasFailedActions", FilterOperator.Equal, true));
        if (this.Truncated) filter.Filters.Add(new("HasTruncation", FilterOperator.Equal, true));
        return filter;
    }
}
