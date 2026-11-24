// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Domain.Model;

/// <summary>Selects portable history policies and bounded LiteDB query settings.</summary>
public sealed class LiteDbChangeHistoryOptions : ChangeHistoryOptions
{
    /// <summary>Gets or sets the isolated history collection name.</summary>
    public string CollectionName { get; set; } = "change_history";

    /// <summary>Gets or sets the maximum rows materialized for grouped queries or a restore plan.</summary>
    public int MaxMaterializedRows { get; set; } = 10000;
}
