namespace BridgingIT.DevKit.Application.Queueing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the shared builder context for queueing registration.
/// </summary>
public class QueueingBuilderContext(
    IServiceCollection services,
    IConfiguration configuration = null,
    QueueingOptions options = null,
    QueueingRegistrationStore registrationStore = null)
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the shared queueing options.
    /// </summary>
    public QueueingOptions Options { get; } = options;

    /// <summary>
    /// Gets the shared registration store.
    /// </summary>
    public QueueingRegistrationStore RegistrationStore { get; } = registrationStore;
}
