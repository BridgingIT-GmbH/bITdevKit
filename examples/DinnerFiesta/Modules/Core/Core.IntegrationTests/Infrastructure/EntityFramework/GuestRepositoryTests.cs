// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Infrastructure;

using System;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class GuestRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CoreDbContext context;

    public GuestRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task GuestTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Guest.Create($"John {ticks}", $"Doe {ticks}", UserId.CreateUnique(), null);

        // Act
        this.context.Guests.Add(entity);
        await this.context.SaveChangesAsync();

        // Assert
        entity.Id.Value.ShouldNotBe(Guid.Empty);
        this.context.Guests.Count().ShouldBeGreaterThanOrEqualTo(1);
    }
}