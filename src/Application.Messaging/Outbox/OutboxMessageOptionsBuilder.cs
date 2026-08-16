// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Builds outbox message options configuration.
/// </summary>
public class OutboxMessageOptionsBuilder : OptionsBuilderBase<OutboxMessageOptions, OutboxMessageOptionsBuilder>
{
    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public OutboxMessageOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Executes the disabled operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="timeSpan">The time span used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Executes the processing interval operation.
    /// </summary>
    /// <param name="timeSpan">The time span used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;

        return this;
    }

    /// <summary>
    /// Executes the processing interval operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the processing interval operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingInterval(string value)
    {
        this.Target.ProcessingInterval = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Executes the processing delay operation.
    /// </summary>
    /// <param name="timeSpan">The time span used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingDelay(TimeSpan timeSpan)
    {
        this.Target.ProcessingDelay = timeSpan;

        return this;
    }

    /// <summary>
    /// Executes the processing delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingDelay(int milliseconds)
    {
        this.Target.ProcessingDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the processing delay operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingDelay(string value)
    {
        this.Target.ProcessingDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Executes the processing jitter operation.
    /// </summary>
    /// <param name="timeSpan">The time span used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingJitter(TimeSpan timeSpan)
    {
        this.Target.ProcessingJitter = timeSpan;

        return this;
    }

    /// <summary>
    /// Executes the processing jitter operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingJitter(int milliseconds)
    {
        this.Target.ProcessingJitter = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the processing jitter operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingJitter(string value)
    {
        this.Target.ProcessingJitter = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Executes the processing mode operation.
    /// </summary>
    /// <param name="mode">The mode used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingMode(OutboxMessageProcessingMode mode)
    {
        this.Target.ProcessingMode = mode;

        return this;
    }

    /// <summary>
    /// Executes the processing mode immediate operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public OutboxMessageOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxMessageProcessingMode.Immediate;
        }

        return this;
    }

    /// <summary>
    /// Executes the purge on startup operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public OutboxMessageOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;

        return this;
    }

    /// <summary>
    /// Executes the purge processed on startup operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public OutboxMessageOptionsBuilder PurgeProcessedOnStartup(bool value = true)
    {
        this.Target.PurgeProcessedOnStartup = value;

        return this;
    }

    /// <summary>
    /// Executes the serializer operation.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Executes the auto save operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public OutboxMessageOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;

        return this;
    }

    /// <summary>
    /// Executes the processing count operation.
    /// </summary>
    /// <param name="count">The number of values to process.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;

        return this;
    }

    /// <summary>
    /// Executes the retry count operation.
    /// </summary>
    /// <param name="retries">The retries used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public OutboxMessageOptionsBuilder RetryCount(int retries)
    {
        this.Target.RetryCount = retries;

        return this;
    }
}
