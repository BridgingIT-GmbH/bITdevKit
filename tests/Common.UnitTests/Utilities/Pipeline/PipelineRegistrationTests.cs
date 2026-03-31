// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class PipelineRegistrationTests
{
    [Fact]
    public void AddPipelines_CanBeCalledMultipleTimes_AndCreateTypedPipelines()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPipelines()
            .WithPipeline<OrdersPipeline>();

        services.AddPipelines()
            .WithPipeline<InventoryPipeline>()
            .WithPipeline<InventoryContext>("inventory-inline", b => b.AddStep(() => { }));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();

        factory.Create<OrdersContext>("orders").ShouldNotBeNull();
        factory.Create<InventoryPipeline>().ShouldNotBeNull();
        factory.Create<InventoryContext>("inventory-inline").ShouldNotBeNull();
    }

    [Fact]
    public void DuplicatePipelineNames_Throw()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPipelines()
            .WithPipeline<OrdersPipeline>()
            .WithPipeline<OrdersPipeline>();

        var provider = services.BuildServiceProvider();

        Should.Throw<PipelineDefinitionValidationException>(() => provider.GetRequiredService<IPipelineFactory>());
    }

    [Fact]
    public void WithPipelinesFromAssembly_RegistersPackagedDefinitions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPipelines()
            .WithPipelinesFromAssembly<OrdersPipeline>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();

        factory.Create<OrdersContext>("orders").ShouldNotBeNull();
    }

    [Fact]
    public void CreateDefinitionWithoutContext_ReturnsPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPipelines()
            .WithPipeline<InventoryPipeline>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();

        factory.Create<InventoryPipeline>().ShouldNotBeNull();
    }

    [Fact]
    public void CreateWithWrongContext_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPipelines()
            .WithPipeline<OrdersPipeline>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();

        Should.Throw<PipelineDefinitionValidationException>(() => factory.Create<InventoryContext>("orders"));
    }

    public sealed class OrdersContext : PipelineContextBase;

    public sealed class InventoryContext : PipelineContextBase;

    public sealed class OrdersPipeline : PipelineDefinition<OrdersContext>
    {
        public override string Name => "orders";

        protected override void Configure(IPipelineDefinitionBuilder<OrdersContext> builder)
        {
            builder.AddStep(() => { });
        }
    }

    public sealed class InventoryPipeline : PipelineDefinition
    {
        protected override void Configure(IPipelineDefinitionBuilder builder)
        {
            builder.AddStep(() => { });
        }
    }

}
