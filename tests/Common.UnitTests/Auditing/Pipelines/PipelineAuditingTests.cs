// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common.UnitTests.Auditing;

using Microsoft.Extensions.DependencyInjection;

public sealed class PipelineAuditingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalBehavior_WithoutCoreOrWithNoOp_InvokesBusinessWorkExactlyOnce(bool noOp)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (noOp) services.AddSingleton<IAuditContext, NoOpAuditContext>();
        var calls = 0;
        services.AddPipelines().WithPipeline<Context>("Import", definition => definition
            .AddBehavior<PipelineAuditingBehavior>().AddBehavior<PipelineAuditingBehavior>().AddStep(() => calls++));
        await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var result = await host.GetRequiredService<IPipelineFactory>().Create<Context>("Import").ExecuteAsync(new());
        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        host.GetServices<IAuditBoundaryRegistrationSource>().SelectMany(source => source.Registrations).ShouldHaveSingleItem().Selection.ShouldBe("selected");
    }
    public sealed class Context : PipelineContextBase { }
}
