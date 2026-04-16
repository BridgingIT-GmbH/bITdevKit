namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public class ServiceProviderMessageHandlerFactoryTests
{
    [Fact]
    public async Task Create_WhenHandlerHasRegisteredDependencies_ReturnsResolvedHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TestDependency>();
        using var provider = services.BuildServiceProvider();
        var sut = new ServiceProviderMessageHandlerFactory(provider);

        // Act
        await using var result = sut.Create(typeof(TestMessageHandler));

        // Assert
        result.Handler.ShouldBeOfType<TestMessageHandler>();
        ((TestMessageHandler)result.Handler).Dependency.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_WhenHandlerUsesScopedDependency_KeepsDependencyAliveUntilResultIsDisposed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        using var provider = services.BuildServiceProvider();
        var sut = new ServiceProviderMessageHandlerFactory(provider);

        // Act
        var result = sut.Create(typeof(ScopedMessageHandler));
        var handler = (ScopedMessageHandler)result.Handler;

        // Assert
        handler.Dependency.IsDisposed.ShouldBeFalse();

        await result.DisposeAsync();

        handler.Dependency.IsDisposed.ShouldBeTrue();
    }

    private sealed class TestDependency;

    private sealed class ScopedDependency : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            this.IsDisposed = true;
        }
    }

    private sealed class TestMessageHandler(TestDependency dependency)
    {
        public TestDependency Dependency { get; } = dependency;
    }

    private sealed class ScopedMessageHandler(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }
}