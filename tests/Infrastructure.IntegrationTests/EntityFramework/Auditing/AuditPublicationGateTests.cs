// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public class AuditPublicationGateTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task SchemaGate_ApprovedSingletonControlTable_HasExactlyFourPhysicalTables(string backend)
    {
        var sut = new AuditPublicationProofStore(backend, await fixture.CreateDatabaseAsync(backend));
        await sut.InitializeAsync();
        var tables = await sut.ReadTablesAsync();

        tables.ShouldBe(["audit_actions", "audit_changes", "audit_control", "audit_records"]);
        // The fourth technical table was explicitly approved on 2026-10-30.
        tables.Count.ShouldBe(4);
    }
}