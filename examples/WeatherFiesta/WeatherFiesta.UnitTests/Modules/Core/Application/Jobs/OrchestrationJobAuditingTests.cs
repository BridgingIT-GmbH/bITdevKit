// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Jobs;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies capture for the sample's constructed orchestration job type.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~OrchestrationJobAuditingTests</code></example>
public class OrchestrationJobAuditingTests
{
    /// <summary>Ensures assembly-qualified generic arguments cannot suppress scheduled-job auditing.</summary>
    /// <example><code>await tests.BeginScope_OrchestrationJobType_RecordsWithoutConfigurationFailures();</code></example>
    [Fact]
    public async Task BeginScope_OrchestrationJobType_RecordsWithoutConfigurationFailures()
    {
        var type = typeof(OrchestrationExecuteJob<Unit, WeatherHelloWorldOrchestration, WeatherHelloWorldOrchestrationData>);
        type.FullName.Length.ShouldBeGreaterThan(512);
        var services = new ServiceCollection();
        services.AddAuditing();
        await using var host = services.BuildServiceProvider();
        await using var servicesScope = host.CreateAsyncScope();
        var context = servicesScope.ServiceProvider.GetRequiredService<IAuditContext>();

        await using (var scope = context.BeginScope(options => options.For(type, "Run").Kind("Job")))
        {
            scope.IsRecording.ShouldBeTrue();
            scope.Complete();
        }

        host.GetRequiredService<AuditRuntime>().SnapshotFailures().ShouldBeEmpty();
        var records = await servicesScope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync();
        records.IsSuccess.ShouldBeTrue();
        var record = records.Value.ShouldHaveSingleItem();
        record.Key.ShouldContain(nameof(WeatherHelloWorldOrchestration));
        record.Key.ShouldContain(nameof(WeatherHelloWorldOrchestrationData));
        record.Key.ShouldEndWith(".Run");
    }
}
