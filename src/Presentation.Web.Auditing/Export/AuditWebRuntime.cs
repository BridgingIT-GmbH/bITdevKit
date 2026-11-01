// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Auditing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Shares bounded presentation metadata per host root, including automatically discovered dashboard plugins.</summary>
/// <example><code>var runtime = AuditWebRuntime.Get(app.Services);</code></example>
public sealed class AuditWebRuntime
{
    private static readonly ConditionalWeakTable<IServiceProvider, AuditWebRuntime> Hosts = new();
    private AuditWebRuntime(IServiceProvider services)
    {
        var options = services.GetRequiredService<AuditExportOptions>(); var time = services.GetRequiredService<TimeProvider>();
        this.Attempts = new(options, time); this.Tokens = new(services.GetRequiredService<IDataProtectionProvider>(), options, time);
    }

    /// <summary>Bounded instance-local descriptor storage.</summary>
    /// <example><code>var store = runtime.Attempts;</code></example>
    public AuditExportAttemptStore Attempts { get; }

    /// <summary>Host-key protected progress tokens.</summary>
    /// <example><code>var tokens = runtime.Tokens;</code></example>
    public AuditExportTokenProtector Tokens { get; }

    /// <summary>Gets the shared runtime using the application's root provider, never a request scope.</summary>
    /// <example><code>var runtime = AuditWebRuntime.Get(app.Services);</code></example>
    public static AuditWebRuntime Get(IServiceProvider rootServices) => Hosts.GetValue(rootServices, services => new(services));
}
