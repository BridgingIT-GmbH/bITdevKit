// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Displays a stable SHA-256 fingerprint instead of a sensitive raw key.
/// </summary>
/// <example>
/// <code>
/// IKeyDisplayStrategy strategy = new Sha256KeyDisplayStrategy();
/// </code>
/// </example>
public sealed class Sha256KeyDisplayStrategy : IKeyDisplayStrategy
{
    /// <inheritdoc />
    public string Display(string key) => string.IsNullOrEmpty(key)
        ? key
        : ContentHashHelper.ComputeSha256(key);
}
