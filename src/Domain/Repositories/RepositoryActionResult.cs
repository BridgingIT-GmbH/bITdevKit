// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.ComponentModel;

/// <summary>
///     Defines the result of a repository action.
/// </summary>
public enum RepositoryActionResult
{
    /// <summary>
    ///     No entity action performed.
    /// </summary>
    [Description("no entity action")]
    None,

    /// <summary>
    ///     The entity was successfully inserted.
    /// </summary>
    [Description("entity inserted")]
    Inserted,

    /// <summary>
    ///     Entity updated
    /// </summary>
    [Description("entity updated")]
    Updated,

    /// <summary>
    ///     Entity was deleted.
    /// </summary>
    [Description("entity deleted")]
    Deleted,

    /// <summary>
    ///     Entity was not found.
    /// </summary>
    [Description("entity not found")]
    NotFound, // not used currently
}