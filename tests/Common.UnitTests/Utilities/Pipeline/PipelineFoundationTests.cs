// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;

[UnitTest("Common")]
public class PipelineFoundationTests
{
    [Fact]
    public void PipelineNameConvention_StripsPipeline_AndUsesKebabCase()
    {
        PipelineNameConvention.FromType(typeof(OrderImportPipeline)).ShouldBe("order-import");
    }

    [Fact]
    public void PipelineStepNameConvention_StripsStep_AndUsesKebabCase()
    {
        PipelineStepNameConvention.FromType(typeof(PersistOrdersStep)).ShouldBe("persist-orders");
    }

    [Fact]
    public void PipelineContextBase_InitializesExecutionContext()
    {
        var context = new TestPipelineContext();

        context.Pipeline.ShouldNotBeNull();
        context.Pipeline.Items.ShouldNotBeNull();
    }

    [Fact]
    public void PipelineControl_Factories_SetOutcome_AndMessage()
    {
        var result = Result.Success().WithMessage("done");

        PipelineControl.Continue(result).Outcome.ShouldBe(PipelineControlOutcome.Continue);
        PipelineControl.Skip(result, "skip").Message.ShouldBe("skip");
        PipelineControl.Retry(result, "retry").Outcome.ShouldBe(PipelineControlOutcome.Retry);
        PipelineControl.Break(result, "break").Outcome.ShouldBe(PipelineControlOutcome.Break);
        PipelineControl.Terminate(result, "terminate").Outcome.ShouldBe(PipelineControlOutcome.Terminate);
    }

    [Fact]
    public void PipelineExecutionOptionsBuilder_HasExpectedDefaults()
    {
        var options = new PipelineExecutionOptionsBuilder().Build();

        options.ContinueOnFailure.ShouldBeFalse();
        options.AccumulateDiagnosticsOnFailure.ShouldBeTrue();
        options.AccumulateDiagnosticsOnBreak.ShouldBeTrue();
        options.MaxRetryAttemptsPerStep.ShouldBe(3);
    }

    [Fact]
    public void PipelineExecutionSnapshot_DefaultsResultToSuccess()
    {
        var snapshot = new PipelineExecutionSnapshot();

        snapshot.Result.IsSuccess.ShouldBeTrue();
    }

    private sealed class TestPipelineContext : PipelineContextBase;

    private sealed class OrderImportPipeline;

    private sealed class PersistOrdersStep;
}
