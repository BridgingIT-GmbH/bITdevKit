// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines the configured path kind for advanced ChangeHistory capture.
/// </summary>
/// <example>
/// <code>
/// var kind = ChangeHistoryCapturePathKind.Owned;
/// </code>
/// </example>
public enum ChangeHistoryCapturePathKind
{
    /// <summary>
    /// A configured owned value-object path.
    /// </summary>
    Owned,

    /// <summary>
    /// A configured identifiable collection path.
    /// </summary>
    Collection,

    /// <summary>
    /// A configured graph include path.
    /// </summary>
    Graph
}
