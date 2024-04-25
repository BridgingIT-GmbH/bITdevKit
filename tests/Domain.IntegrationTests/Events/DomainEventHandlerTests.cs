// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Events;

using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Domain")]
public class DomainEventHandlerTests : TestsBase
{
    private readonly IMediator mediator;

    public DomainEventHandlerTests(ITestOutputHelper output)
        : base(output, s => s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies().Where(a =>
            !a.GetName().Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)).ToArray())))
    {
        this.mediator = this.ServiceProvider.GetService<IMediator>();
    }

    [Fact]
    public async Task DomainEventHandler_Test()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        await this.mediator.Publish(
            new StubPersonAddedDomainEvent(id)).AnyContext();

        // Assert
        StubPersonAddedDomainEventHandler.Handled.ShouldBeTrue();
        StubPersonAddedDomainEventHandler.PersonId.ShouldBe(id);
    }
}