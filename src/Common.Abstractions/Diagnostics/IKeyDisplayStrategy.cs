// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Formats application keys for diagnostics without changing persisted keys.
/// </summary>
/// <example>
/// <code>
/// var displayed = strategy.Display("customers/42");
/// </code>
/// </example>
public interface IKeyDisplayStrategy
{
    /// <summary>Formats a key for display.</summary>
    /// <param name="key">The raw key.</param>
    /// <returns>The display value.</returns>
    /// <example><code>var displayed = strategy.Display(key);</code></example>
    string Display(string key);
}
