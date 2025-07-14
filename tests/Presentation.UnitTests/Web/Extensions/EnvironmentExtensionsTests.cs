namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using Microsoft.Extensions.Hosting;

public class EnvironmentExtensionsTests
{
    private readonly IHostEnvironment env;
    private const string AZURE_WEBSITES_ENV = "WEBSITE_SITE_NAME";
    private const string AZURE_FUNCTIONS_ENV = "AZURE_FUNCTIONS_ENVIRONMENT";

    public EnvironmentExtensionsTests()
    {
        this.env = Substitute.For<IHostEnvironment>();
        this.ResetEnvironmentVariables();
        this.env.EnvironmentName.Returns("Development");
    }

    private void ResetEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", null);
        Environment.SetEnvironmentVariable(AZURE_WEBSITES_ENV, null);
        Environment.SetEnvironmentVariable(AZURE_FUNCTIONS_ENV, null);
    }

    [Fact]
    public void IsDocker_WhenContainerEnvVar_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");
        this.env.IsDocker().ShouldBeTrue();
    }

    [Fact]
    public void IsDocker_WhenNoContainerEnvVar_ReturnsFalse()
    {
        this.env.IsDocker().ShouldBeFalse();
    }

    [Fact]
    public void IsKubernetes_WhenK8sEnvVar_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "test");
        this.env.IsKubernetes().ShouldBeTrue();
    }

    [Fact]
    public void IsKubernetes_WhenNoK8sEnvVar_ReturnsFalse()
    {
        this.env.IsKubernetes().ShouldBeFalse();
    }

    [Fact]
    public void IsAzure_WhenWebsiteEnvVar_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(AZURE_WEBSITES_ENV, "test");
        this.env.IsAzure().ShouldBeTrue();
    }

    [Fact]
    public void IsAzure_WhenNoWebsiteEnvVar_ReturnsFalse()
    {
        this.env.IsAzure().ShouldBeFalse();
    }

    [Fact]
    public void IsAzureFunctions_WhenFunctionsEnvVar_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(AZURE_FUNCTIONS_ENV, "test");
        this.env.IsAzureFunctions().ShouldBeTrue();
    }

    [Fact]
    public void IsAzureFunctions_WhenNoFunctionsEnvVar_ReturnsFalse()
    {
        this.env.IsAzureFunctions().ShouldBeFalse();
    }

    [Fact]
    public void IsCloud_WhenAzure_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(AZURE_WEBSITES_ENV, "test");
        this.env.IsCloud().ShouldBeTrue();
    }

    [Fact]
    public void IsCloud_WhenNoAzure_ReturnsFalse()
    {
        this.env.IsCloud().ShouldBeFalse();
    }

    [Fact]
    public void IsContainerized_WhenDocker_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");
        this.env.IsContainerized().ShouldBeTrue();
    }

    [Fact]
    public void IsContainerized_WhenK8s_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "test");
        this.env.IsContainerized().ShouldBeTrue();
    }

    [Fact]
    public void IsContainerized_WhenNoContainerOrK8s_ReturnsFalse()
    {
        this.env.IsContainerized().ShouldBeFalse();
    }

    [Fact]
    public void IsLocalDevelopment_WhenDevAndNoContainerAndNoCloud_ReturnsTrue()
    {
        this.env.EnvironmentName.Returns("Development");
        this.env.IsLocalDevelopment().ShouldBeTrue();
    }

    [Fact]
    public void IsLocalDevelopment_WhenDevAndContainer_ReturnsFalse()
    {
        this.env.EnvironmentName.Returns("Development");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");
        this.env.IsLocalDevelopment().ShouldBeFalse();
    }

    [Fact]
    public void IsLocalDevelopment_WhenDevAndCloud_ReturnsFalse()
    {
        this.env.EnvironmentName.Returns("Development");
        Environment.SetEnvironmentVariable(AZURE_WEBSITES_ENV, "test");
        this.env.IsLocalDevelopment().ShouldBeFalse();
    }
}