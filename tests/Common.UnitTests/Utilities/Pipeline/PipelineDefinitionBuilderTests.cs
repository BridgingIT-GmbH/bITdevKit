// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;

[UnitTest("Common")]
public class PipelineDefinitionBuilderTests
{
    [Fact]
    public void Build_ClassBasedStep_UsesConventionName()
    {
        var definition = new PipelineDefinitionBuilder<TestContext>("test-pipeline")
            .AddStep<ValidateOrderImportStep>()
            .Build();

        definition.Steps.Count.ShouldBe(1);
        definition.Steps[0].Name.ShouldBe("validate-order-import");
        definition.ContextType.ShouldBe(typeof(TestContext));
    }

    [Fact]
    public void Build_InlineStep_UsesGeneratedName()
    {
        var definition = new PipelineDefinitionBuilder("test-pipeline")
            .AddStep(() => { })
            .AddStep(() => { })
            .Build();

        definition.Steps[0].Name.ShouldBe("inline-step-1");
        definition.Steps[1].Name.ShouldBe("inline-step-2");
    }

    [Fact]
    public void Build_DisabledStep_IsExcluded()
    {
        var definition = new PipelineDefinitionBuilder("test-pipeline")
            .AddStep<ValidateOrderImportStep>(enabled: false)
            .Build();

        definition.Steps.ShouldBeEmpty();
    }

    [Fact]
    public void Build_DisabledHookAndBehavior_AreExcluded()
    {
        var definition = new PipelineDefinitionBuilder("test-pipeline")
            .AddHook<TestHook>(enabled: false)
            .AddBehavior<TestBehavior>(enabled: false)
            .Build();

        definition.HookTypes.ShouldBeEmpty();
        definition.BehaviorTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Build_DuplicateStepNames_Throws()
    {
        Should.Throw<PipelineDefinitionValidationException>(() =>
            new PipelineDefinitionBuilder("test-pipeline")
                .AddStep(() => { }, name: "duplicate")
                .AddStep(() => { }, name: "duplicate")
                .Build());
    }

    [Fact]
    public void Build_WhenConditionIsFalse_ExcludesStep()
    {
        var definition = new PipelineDefinitionBuilder("test-pipeline")
            .AddStep(
                () => { },
                configure: b => b.When(new FalseCondition()))
            .Build();

        definition.Steps.ShouldBeEmpty();
    }

    [Fact]
    public void PipelineDefinition_NonGenericBuild_UsesConventionName()
    {
        var definition = new FileCleanupPipeline().Build();

        definition.Name.ShouldBe("file-cleanup");
        definition.ContextType.ShouldBe(typeof(NullPipelineContext));
    }

    [Fact]
    public void PipelineDefinition_GenericBuild_UsesConventionName()
    {
        var definition = new OrderImportPipeline().Build();

        definition.Name.ShouldBe("order-import");
        definition.ContextType.ShouldBe(typeof(TestContext));
    }

    private sealed class FalseCondition : IPipelineDefinitionCondition
    {
        public bool IsSatisfied(PipelineDefinitionContext context) => false;
    }

    private sealed class TestContext : PipelineContextBase;

    private sealed class ValidateOrderImportStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options) =>
            PipelineControl.Continue(result);
    }

    private sealed class TestHook : PipelineHook<TestContext>;

    private sealed class TestBehavior : IPipelineBehavior<TestContext>
    {
        public ValueTask<Result> ExecuteAsync(TestContext context, Func<ValueTask<Result>> next, CancellationToken cancellationToken) =>
            next();
    }

    private sealed class FileCleanupPipeline : PipelineDefinition
    {
        protected override void Configure(IPipelineDefinitionBuilder builder)
        {
            builder.AddStep(() => { });
        }
    }

    private sealed class OrderImportPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<ValidateOrderImportStep>();
        }
    }
}
