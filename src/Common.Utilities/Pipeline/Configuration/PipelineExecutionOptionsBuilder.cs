// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Builds immutable <see cref="PipelineExecutionOptions"/> instances.
/// </summary>
public class PipelineExecutionOptionsBuilder : IPipelineExecutionOptionsBuilder
{
    private readonly PipelineExecutionOptions options = new();

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder WithProgress(IProgress<ProgressReport> progress)
    {
        this.options.Progress = progress;
        return this;
    }

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder WhenCompleted(Func<PipelineCompletion, ValueTask> callback)
    {
        this.options.CompletionCallback = callback;
        return this;
    }

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder ContinueOnFailure(bool value = true)
    {
        this.options.ContinueOnFailure = value;
        return this;
    }

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnFailure(bool value = true)
    {
        this.options.AccumulateDiagnosticsOnFailure = value;
        return this;
    }

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder AccumulateDiagnosticsOnBreak(bool value = true)
    {
        this.options.AccumulateDiagnosticsOnBreak = value;
        return this;
    }

    /// <inheritdoc />
    public IPipelineExecutionOptionsBuilder MaxRetryAttemptsPerStep(int value)
    {
        this.options.MaxRetryAttemptsPerStep = value;
        return this;
    }

    /// <inheritdoc />
    public PipelineExecutionOptions Build()
    {
        return new PipelineExecutionOptions
        {
            Progress = this.options.Progress,
            CompletionCallback = this.options.CompletionCallback,
            ContinueOnFailure = this.options.ContinueOnFailure,
            AccumulateDiagnosticsOnFailure = this.options.AccumulateDiagnosticsOnFailure,
            AccumulateDiagnosticsOnBreak = this.options.AccumulateDiagnosticsOnBreak,
            MaxRetryAttemptsPerStep = this.options.MaxRetryAttemptsPerStep
        };
    }
}
