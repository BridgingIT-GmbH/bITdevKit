// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>Records the frozen provider-neutral format-one annotated cache model.</summary>
/// <example><code>var model = new CacheTestHostDbContextModelSnapshot().Model;</code></example>
[DbContext(typeof(CacheTestHostDbContext))]
public sealed class CacheTestHostDbContextModelSnapshot : ModelSnapshot
{
    /// <inheritdoc />
    protected override void BuildModel(ModelBuilder modelBuilder) => CacheTestHostModel.Configure(modelBuilder);
}