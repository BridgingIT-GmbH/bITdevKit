// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Finite event-range and execution budgets shared by all audit query consumers.</summary>
/// <example><code>builder.WithQueries(new() { MaxRange = TimeSpan.FromDays(7) });</code></example>
public sealed record AuditQueryOptions
{
    /// <summary>Maximum number of executions returned by one search or resource-history page.</summary>
    /// <example><code>var filter = new FilterModel { PageSize = AuditQueryOptions.MAX_PAGE_SIZE };</code></example>
    public const int MAX_PAGE_SIZE = 500;

    /// <summary>The default event-start range ending at the operation's as-of instant.</summary>
    /// <example><code>var range = options.DefaultRange;</code></example>
    public TimeSpan DefaultRange { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Maximum explicit search/history range, capped at one year.</summary>
    /// <example><code>var range = options.MaxRange;</code></example>
    public TimeSpan MaxRange { get; init; } = TimeSpan.FromDays(31);

    /// <summary>Deadline for scope resolution and all provider work in one query operation.</summary>
    /// <example><code>var timeout = options.Timeout;</code></example>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Validates bounded configuration without reading the store.</summary>
    /// <example><code>options.Validate();</code></example>
    public void Validate()
    {
        if (this.DefaultRange <= TimeSpan.Zero || this.MaxRange < this.DefaultRange || this.MaxRange > TimeSpan.FromDays(366) ||
            this.Timeout <= TimeSpan.Zero || this.Timeout > TimeSpan.FromMinutes(1)) throw new ArgumentException("Audit query limits are invalid.");
    }
}
