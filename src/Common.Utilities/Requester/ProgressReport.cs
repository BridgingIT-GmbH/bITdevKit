// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a progress report for request or notification processing.
/// </summary>
/// <remarks>
/// This class is used to report progress through <see cref="IProgress{ProgressReport}"/> in <see cref="SendOptions"/>
/// or <see cref="PublishOptions"/>. It includes the operation name, progress messages, percentage complete, and
/// completion status, allowing handlers and behaviors to provide feedback during processing.
/// </remarks>
/// <example>
/// <code>
/// options.Progress?.Report(new ProgressReport(
///     "SampleRequest",
///     new[] { "Processing request" },
///     50.0));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="ProgressReport"/> class.
/// </remarks>
/// <param name="operation">The name of the operation being reported.</param>
/// <param name="messages">The messages describing the current progress.</param>
/// <param name="percentageComplete">The percentage of completion (0.0 to 100.0).</param>
/// <param name="isCompleted">Indicates whether the operation is fully completed.</param>
public class ProgressReport(string operation, IEnumerable<string> messages, double percentageComplete, bool isCompleted = false)
{
    /// <summary>
    /// Gets the name of the operation being reported.
    /// </summary>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets the messages describing the current progress.
    /// </summary>
    public string[] Messages { get; } = messages?.ToArray() ?? [];

    /// <summary>
    /// Gets the percentage of completion, ranging from 0.0 to 100.0.
    /// </summary>
    public double PercentageComplete { get; } = percentageComplete;

    /// <summary>
    /// Gets a value indicating whether the operation is fully completed.
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;
}
