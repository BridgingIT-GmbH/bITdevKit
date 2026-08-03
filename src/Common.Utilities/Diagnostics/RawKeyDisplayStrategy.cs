// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Displays keys unchanged for normal operational diagnostics.
/// </summary>
/// <example>
/// <code>
/// IKeyDisplayStrategy strategy = new RawKeyDisplayStrategy();
/// </code>
/// </example>
public sealed class RawKeyDisplayStrategy : IKeyDisplayStrategy
{
    /// <inheritdoc />
    public string Display(string key) => key;
}
