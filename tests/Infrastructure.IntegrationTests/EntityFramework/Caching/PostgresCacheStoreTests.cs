// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Caching;

using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public class PostgresCacheStoreTests(AuditRelationalFixture fixture) : EntityFrameworkCacheStoreTestsBase(fixture)
{
    protected override string Engine => "Postgres";
}