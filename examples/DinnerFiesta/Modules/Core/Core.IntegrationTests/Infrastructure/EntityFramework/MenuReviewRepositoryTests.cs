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
public class MenuReviewRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CoreDbContext context;

    public MenuReviewRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task MenuReviewTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = MenuReview.Create(5, $"test comment {ticks}", HostId.Create(), MenuId.Create(), GuestId.Create(), DinnerId.Create());

        // Act
        this.context.MenuReviews.Add(entity);
        await this.context.SaveChangesAsync();

        // Assert
        entity.Id.Value.ShouldNotBe(Guid.Empty);
        this.context.MenuReviews.Count().ShouldBeGreaterThanOrEqualTo(1);
    }
}