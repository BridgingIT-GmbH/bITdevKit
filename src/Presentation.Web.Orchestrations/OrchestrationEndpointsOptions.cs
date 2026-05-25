namespace BridgingIT.DevKit.Presentation.Web.Orchestrations;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the operational orchestration endpoint group.
/// </summary>
public class OrchestrationEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationEndpointsOptions"/> class with orchestration-specific defaults.
    /// </summary>
    public OrchestrationEndpointsOptions()
    {
        this.GroupPath = "/api/_system/orchestrations";
        this.GroupTag = "_System.Orchestrations";
    }
}