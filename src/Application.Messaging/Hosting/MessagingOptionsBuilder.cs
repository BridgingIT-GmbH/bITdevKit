// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Builds messaging options configuration.
/// </summary>
public class MessagingOptionsBuilder : OptionsBuilderBase<MessagingOptions, MessagingOptionsBuilder>
{
    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public MessagingOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Executes the disabled operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public MessagingOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="timespan">The timespan used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public MessagingOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public MessagingOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Executes the startup delay operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public MessagingOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }
}
