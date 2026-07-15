// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Microsoft.EntityFrameworkCore;

internal sealed class EntityBulkInsertConfiguration<TEntity, TContext>(EntityBulkInsertOptions options)
    where TEntity : class
    where TContext : DbContext
{
    public EntityBulkInsertOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));
}
