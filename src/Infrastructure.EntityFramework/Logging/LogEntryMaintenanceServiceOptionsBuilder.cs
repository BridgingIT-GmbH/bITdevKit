// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Builds log entry maintenance options configuration.
/// </summary>
/// <example>
/// <code>
/// services.AddLogEntryServices&lt;CoreDbContext&gt;(o => o
///     .Enabled(builder.Environment.IsDevelopment())
///     .CleanupDeleteOlderThanDays(14));
/// </code>
/// </example>
public class LogEntryMaintenanceServiceOptionsBuilder
    : OptionsBuilderBase<LogEntryMaintenanceServiceOptions, LogEntryMaintenanceServiceOptionsBuilder>
{
    /// <summary>
    /// Enables or disables the log entry services.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Disables the log entry services.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    /// Sets the delay before the maintenance processing starts.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    /// Sets the delay before the maintenance processing starts.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Sets the interval at which queued maintenance requests are processed.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder ProcessingInterval(TimeSpan timespan)
    {
        this.Target.ProcessingInterval = timespan;

        return this;
    }

    /// <summary>
    /// Sets the interval at which the periodic cleanup runs.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder CleanupInterval(TimeSpan timespan)
    {
        this.Target.CleanupInterval = timespan;

        return this;
    }

    /// <summary>
    /// Sets the age in days after which logs are archived by the periodic cleanup.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder CleanupArchiveOlderThanDays(int days)
    {
        this.Target.CleanupArchiveOlderThanDays = days;

        return this;
    }

    /// <summary>
    /// Sets the age in days after which archived logs are deleted by the periodic cleanup.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder CleanupDeleteOlderThanDays(int days)
    {
        this.Target.CleanupDeleteOlderThanDays = days;

        return this;
    }

    /// <summary>
    /// Sets the number of items processed in a single cleanup batch.
    /// </summary>
    public LogEntryMaintenanceServiceOptionsBuilder CleanupBatchSize(int size)
    {
        this.Target.CleanupBatchSize = size;

        return this;
    }
}
