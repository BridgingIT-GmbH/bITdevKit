// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Microsoft.EntityFrameworkCore;
using Models;

/// <summary>
/// Represents db initializer.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Executes the initialize event store db context operation.
    /// </summary>
    /// <param name="dbContext">The db context used by the operation.</param>
    public static void InitializeEventStoreDbContext(EventStoreDbContext dbContext)
    {
        EnsureArg.IsNotNull(dbContext, nameof(dbContext));
#if DEBUG
        dbContext.Database.Migrate();
#endif
    }
}
