// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

/// <summary>
/// Defines operations for i lite db context.
/// </summary>
public interface ILiteDbContext : IDisposable
{
    /// <summary>
    /// Gets the database.
    /// </summary>
    LiteDatabase Database { get; }
}
