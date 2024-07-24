namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System;
using System.Linq;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EnumerationConverterTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public void Can_Save_And_Retrieve_Enumeration()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 32, Status.Active);

        var context = this.fixture.EnsureSqlServerDbContext(this.output, true);
        context.Persons.Add(entity);
        context.SaveChanges();

        context = this.fixture.EnsureSqlServerDbContext(this.output, true);
        var retrievedEntity = context.Persons.Single(e => e.Id == entity.Id);
        retrievedEntity.Status.ShouldBe(Status.Active);
    }
}