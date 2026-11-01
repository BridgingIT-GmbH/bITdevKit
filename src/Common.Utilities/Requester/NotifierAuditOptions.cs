// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Local notification selection, used only after explicit Notifier audit behavior registration.</summary>
/// <example><code>notifier.WithAuditing(options => options with { Select = type => type == typeof(CustomerRenamedNotification) });</code></example>
public sealed record NotifierAuditOptions
{
    /// <summary>Selects notification types without serializing their payloads.</summary>
    /// <example><code>var selected = options.Select(typeof(CustomerRenamedNotification));</code></example>
    public Func<Type, bool> Select { get; init; } = _ => true;
}
