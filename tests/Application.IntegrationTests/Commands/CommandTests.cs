// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Commands;

using MediatR;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Application")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CommandTests(ITestOutputHelper output) : TestsBase(output, s =>
        {
            s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                !a.GetName().Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)).ToArray()));
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(Application.Commands.DummyCommandBehavior<,>));
        })
{
    [Fact]
    public void CreatePersonCommand()
    {
        var entity = new PersonStub();
        var command = new StubPersonAddCommand(entity);

        command.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAndHandleCommand_Test()
    {
        // Arrange
        var mediator = this.ServiceProvider.GetRequiredService<IMediator>();
        var entity = new PersonStub() { FirstName = "John", LastName = "Doe" };
        var command = new StubPersonAddCommand(entity);

        // Act
        var response = await mediator.Send(command).AnyContext();

        // Assert
        command.ShouldNotBeNull();
        response.ShouldNotBeNull();
        response.Result.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePersonCommand() // fails often? (Could not load type 'Castle.Proxies.ObjectProxy' from assembly 'DynamicProxyGenAssembly2....)
    {
        var entity = new PersonStub() { FirstName = "John", LastName = "Doe" };
        var command = new StubPersonAddCommand(entity);

        command.Validate().IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePersonCommandFails()
    {
        var entity = new PersonStub();
        var command = new StubPersonAddCommand(entity);

        command.Validate().IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidatePersonCommandNoFirstnameFails()
    {
        var entity = new PersonStub
        {
            LastName = "Doe"
        };

        var command = new StubPersonAddCommand(entity);

        command.Validate().IsValid.ShouldBeFalse();
    }
}