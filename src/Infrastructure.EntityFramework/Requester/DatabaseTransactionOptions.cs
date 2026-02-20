// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Options for configuring database transaction behavior.
/// </summary>
public class DatabaseTransactionOptions
{
    /// <summary>
    /// Gets or sets the default DbContext name to use when the attribute doesn't specify one.
    /// Can omit the "DbContext" suffix (e.g., "Core" or "CoreDbContext").
    /// </summary>
    public string DefaultContextName { get; set; }
}
