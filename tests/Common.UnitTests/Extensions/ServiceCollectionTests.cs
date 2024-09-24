namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class ServiceCollectionExtensionsTests
{
    private interface ITestService { }

    [Fact]
    public void IsAdded_WhenServiceIsAdded_ShouldReturnTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, StubService>();

        // Act
        var result = services.IsAdded<ITestService>();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsAdded_WhenServiceIsNotAdded_ShouldReturnFalse()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.IsAdded<ITestService>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsAdded_WhenServiceCollectionIsNull_ShouldReturnFalse()
    {
        // Arrange
        IServiceCollection services = null;

        // Act
        var result = services.IsAdded<ITestService>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsAdded_WhenServiceCollectionIsEmpty_ShouldReturnFalse()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.IsAdded<ITestService>();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Find_WhenServiceIsAdded_ShouldReturnServiceDescriptor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, StubService>();

        // Act
        var result = services.Find<ITestService>();

        // Assert
        result.ShouldNotBeNull();
        result.ServiceType.ShouldBe(typeof(ITestService));
        result.ImplementationType.ShouldBe(typeof(StubService));
    }

    [Fact]
    public void Find_WhenServiceIsNotAdded_ShouldReturnNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.Find<ITestService>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_WhenServiceCollectionIsNull_ShouldReturnNull()
    {
        // Arrange
        IServiceCollection services = null;

        // Act
        var result = services.Find<ITestService>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_WhenServiceCollectionIsEmpty_ShouldReturnNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.Find<ITestService>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void IndexOf_WhenServiceIsAdded_ShouldReturnCorrectIndex()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, StubService>();

        // Act
        var result = services.IndexOf<ITestService>();

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void IndexOf_WhenServiceIsNotAdded_ShouldReturnNegativeOne()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.IndexOf<ITestService>();

        // Assert
        result.ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_WhenServiceCollectionIsNull_ShouldReturnZero()
    {
        // Arrange
        IServiceCollection services = null;

        // Act
        var result = services.IndexOf<ITestService>();

        // Assert
        result.ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_WhenServiceCollectionIsEmpty_ShouldReturnNegativeOne()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.IndexOf<ITestService>();

        // Assert
        result.ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_WhenMultipleServicesAreAdded_ShouldReturnCorrectIndex()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(_ => "test0");
        services.AddTransient(_ => "test1");
        services.AddTransient<ITestService, StubService>();
        services.AddTransient(_ => "test3");

        // Act
        var result = services.IndexOf<ITestService>();

        // Assert
        result.ShouldBe(2);
    }

    private class StubService : ITestService { }
}