// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Defines read-only Close or editor Cancel/submit actions for either dialog adapter.</summary>
/// <param name="ActionText">The specific submit action, or null for read-only details.</param>
/// <param name="ActionId">The optional stable submit identifier.</param>
/// <param name="Destructive">Whether the submit action is destructive.</param>
/// <param name="Native">Whether the containing frame is a native dialog.</param>
/// <example><code>new DashboardModalActionsModel("Save changes", Native: true);</code></example>
public sealed record DashboardModalActionsModel(string ActionText = null, string ActionId = null, bool Destructive = false, bool Native = false);
