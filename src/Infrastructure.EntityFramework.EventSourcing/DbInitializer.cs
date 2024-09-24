// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using Microsoft.EntityFrameworkCore;
using Models;

public static class DbInitializer
{
    public static void InitializeEventStoreDbContext(EventStoreDbContext dbContext)
    {
        EnsureArg.IsNotNull(dbContext, nameof(dbContext));
#if DEBUG
        dbContext.Database.Migrate();
#endif
    }
}