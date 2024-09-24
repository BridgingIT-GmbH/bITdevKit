// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using Application.Queries;
using MediatR;

[IntegrationTest("Application")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class QueryTests(ITestOutputHelper output) : TestsBase(output,
    s =>
    {
        s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
                !a.GetName()
                    .Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            .ToArray()));
        s.AddTransient(typeof(IPipelineBehavior<,>), typeof(DummyQueryBehavior<,>));
    })
{
    //[Fact]
    //public async Task InvalidQueryHandler_Test()
    //{
    //    // Arrange
    //    var mediator = this.ServiceProvider.GetService<IMediator>();
    //    var query = new StubPersonQuery(string.Empty);

    //    // Act
    //    TODO: refactor to use Shouldly
    //    var ex = await Assert.ThrowsAsync<ValidationException>(async () => await mediator.Send(query).AnyContext()).AnyContext();

    //    // Assert
    //    query.ShouldNotBeNull();
    //}

    [Fact]
    public async Task CreateAndHandleQuery_Test()
    {
        // Arrange
        var mediator = this.ServiceProvider.GetService<IMediator>();

        var query = new StubPersonQuery("John");

        // Act
        var response = await mediator.Send(query)
            .AnyContext();

        // Assert
        response.Result.Count()
            .ShouldBe(2);
        response.Result.ShouldAllBe(x => x.FirstName == "John");
        response.Cancelled.ShouldBeFalse();
    }
}