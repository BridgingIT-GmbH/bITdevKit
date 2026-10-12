// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Identifies the editable UTC inputs populated by a dashboard quick range control.</summary>
/// <example><code>var range = new DashboardDateRangeModel("logs-from", "logs-to");</code></example>
/// <param name="FromInputId">The inclusive start input's DOM identifier.</param>
/// <param name="ToInputId">The end input's DOM identifier.</param>
public sealed record DashboardDateRangeModel(string FromInputId, string ToInputId);
