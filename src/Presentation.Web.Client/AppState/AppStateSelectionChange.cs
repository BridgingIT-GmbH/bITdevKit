// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Describes one selected-value transition and its originating version.</summary>
/// <typeparam name="TSelected">The selected value type.</typeparam>
/// <param name="Previous">The independently captured previous selection.</param>
/// <param name="Current">The independently captured current selection.</param>
/// <param name="Version">The version to check before applying asynchronous results.</param>
/// <example>
/// <code><![CDATA[
/// using var subscription = state.Subscribe(model => model.Page, (change, token) =>
/// {
///     AppStateSelectionChange<int> selection = change;
///     Debug.Assert(selection.Current != selection.Previous);
///     return Task.CompletedTask;
/// });
/// await state.SetPage(2);
/// ]]></code>
/// </example>
public sealed record AppStateSelectionChange<TSelected>(TSelected Previous, TSelected Current, AppStateVersion Version);
