// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.ComponentModel;

public enum RepositoryActionResult
{
    /// <summary>
    /// Nonde
    /// </summary>
    [Description("no entity action")]
    None,

    /// <summary>
    /// Inserted
    /// </summary>
    [Description("entity inserted")]
    Inserted,

    /// <summary>
    /// Updated
    /// </summary>
    [Description("entity updated")]
    Updated,

    /// <summary>
    /// Deleted
    /// </summary>
    [Description("entity deleted")]
    Deleted
}