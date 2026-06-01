// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Builds a code-first trigger definition.
/// </summary>
/// <example>
/// <code>
/// var builder = new JobTriggerDefinitionBuilder("manual", "cleanup", typeof(Unit));
/// builder.Manual().Enabled();
/// </code>
/// </example>
public class JobTriggerDefinitionBuilder
{
    private readonly Dictionary<string, string> properties = [];
    private readonly List<string> targetInstances = [];
    private readonly Type jobDataType;
    private JobTriggerType triggerType = JobTriggerType.Manual;
    private bool enabled = true;
    private int? priority;
    private TimeSpan? timeout;
    private JobRetryPolicy retryPolicy;
    private object data = Unit.Value;
    private Type dataType;
    private string schedule;
    private DateTimeOffset? dueUtc;
    private TimeSpan? delay;
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private JobMissedOccurrencePolicy missedOccurrencePolicy = JobMissedOccurrencePolicy.Skip;
    private Type customTriggerProviderType;
    private string eventSource;
    private Type eventDataType;
    private JobCalendarDefinition calendar;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobTriggerDefinitionBuilder"/> class.
    /// </summary>
    /// <param name="triggerName">The stable trigger name.</param>
    /// <param name="jobName">The owning job name.</param>
    /// <param name="jobDataType">The owning job data contract.</param>
    public JobTriggerDefinitionBuilder(
        string triggerName,
        string jobName,
        Type jobDataType)
    {
        this.TriggerName = string.IsNullOrWhiteSpace(triggerName)
            ? throw new InvalidOperationException($"The job '{jobName}' requires non-empty trigger names.")
            : triggerName.Trim();
        this.JobName = jobName;
        this.jobDataType = jobDataType ?? typeof(Unit);
        this.dataType = this.jobDataType;
    }

    /// <summary>
    /// Gets the stable trigger name.
    /// </summary>
    public string TriggerName { get; }

    /// <summary>
    /// Gets the owning job name.
    /// </summary>
    public string JobName { get; }

    /// <summary>
    /// Configures a manual trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder Manual()
    {
        this.triggerType = JobTriggerType.Manual;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.schedule = null;
        this.dueUtc = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Configures a cron trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder Cron(string expression)
    {
        this.triggerType = JobTriggerType.Cron;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.schedule = string.IsNullOrWhiteSpace(expression)
            ? throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a non-empty cron expression.")
            : expression.Trim();
        this.dueUtc = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Configures a scheduler-owned calendar trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder Calendar(Action<JobCalendarDefinitionBuilder> configure)
    {
        var builder = new JobCalendarDefinitionBuilder();
        configure?.Invoke(builder);

        this.triggerType = JobTriggerType.Calendar;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = builder.Build();
        this.schedule = this.calendar.ToScheduleExpression();
        this.dueUtc = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Configures a one-time trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder At(DateTimeOffset value)
    {
        this.triggerType = JobTriggerType.OneTime;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.dueUtc = value.ToUniversalTime();
        this.schedule = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Configures a delayed trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder After(TimeSpan value)
    {
        this.triggerType = JobTriggerType.Delayed;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.delay = value;
        this.schedule = null;
        this.dueUtc = null;
        return this;
    }

    /// <summary>
    /// Configures a startup-delay trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder StartupDelay(TimeSpan value)
    {
        this.triggerType = JobTriggerType.StartupDelay;
        this.customTriggerProviderType = null;
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.delay = value;
        this.schedule = null;
        this.dueUtc = null;
        return this;
    }

    /// <summary>
    /// Configures an event-backed trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder Event<TEvent>(string source)
    {
        this.triggerType = JobTriggerType.Event;
        this.customTriggerProviderType = null;
        this.calendar = null;
        this.eventSource = string.IsNullOrWhiteSpace(source)
            ? throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a non-empty event source.")
            : source.Trim();
        this.eventDataType = typeof(TEvent);
        this.schedule = null;
        this.dueUtc = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Configures a custom trigger provider.
    /// </summary>
    public JobTriggerDefinitionBuilder Custom<TProvider>()
        where TProvider : class
    {
        this.triggerType = JobTriggerType.Custom;
        this.customTriggerProviderType = typeof(TProvider);
        this.eventSource = null;
        this.eventDataType = null;
        this.calendar = null;
        this.schedule = null;
        this.dueUtc = null;
        this.delay = null;
        return this;
    }

    /// <summary>
    /// Sets the trigger enabled state.
    /// </summary>
    public JobTriggerDefinitionBuilder Enabled(bool value = true)
    {
        this.enabled = value;
        return this;
    }

    /// <summary>
    /// Sets the time zone used for schedule calculation.
    /// </summary>
    public JobTriggerDefinitionBuilder InTimeZone(TimeZoneInfo value)
    {
        this.timeZone = value ?? throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a time zone.");
        return this;
    }

    /// <summary>
    /// Sets the time zone used for schedule calculation.
    /// </summary>
    public JobTriggerDefinitionBuilder TimeZone(TimeZoneInfo value)
    {
        return this.InTimeZone(value);
    }

    /// <summary>
    /// Sets the missed-occurrence policy.
    /// </summary>
    public JobTriggerDefinitionBuilder WithMissedOccurrencePolicy(JobMissedOccurrencePolicy value)
    {
        this.missedOccurrencePolicy = value;
        return this;
    }

    /// <summary>
    /// Sets a trigger-specific priority override.
    /// </summary>
    public JobTriggerDefinitionBuilder Priority(int value)
    {
        this.priority = value;
        return this;
    }

    /// <summary>
    /// Sets a trigger-specific timeout override.
    /// </summary>
    public JobTriggerDefinitionBuilder Timeout(TimeSpan value)
    {
        this.timeout = value;
        return this;
    }

    /// <summary>
    /// Sets a trigger-specific retry policy.
    /// </summary>
    public JobTriggerDefinitionBuilder Retry(Action<JobRetryPolicyBuilder> configure)
    {
        var builder = new JobRetryPolicyBuilder();
        configure?.Invoke(builder);
        this.retryPolicy = builder.Build();
        return this;
    }

    /// <summary>
    /// Sets the default trigger data.
    /// </summary>
    public JobTriggerDefinitionBuilder Data(object value)
    {
        this.data = value ?? Unit.Value;
        this.dataType = value?.GetType() ?? typeof(Unit);
        return this;
    }

    /// <summary>
    /// Sets trigger properties.
    /// </summary>
    public JobTriggerDefinitionBuilder WithProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires non-empty property keys.");
        }

        this.properties[key.Trim()] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the eligible scheduler instance targets for this trigger.
    /// </summary>
    public JobTriggerDefinitionBuilder TargetInstances(params string[] values)
    {
        this.targetInstances.Clear();
        foreach (var value in values.SafeNull())
        {
            var target = value?.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!this.targetInstances.Contains(target, StringComparer.OrdinalIgnoreCase))
            {
                this.targetInstances.Add(target);
            }
        }

        if (values is not null && values.Length > 0 && this.targetInstances.Count == 0)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires at least one non-empty scheduler instance target when targeting is configured.");
        }

        return this;
    }

    /// <summary>
    /// Builds the immutable trigger definition.
    /// </summary>
    public JobTriggerDefinition Build()
    {
        if (this.dataType != typeof(Unit) && this.dataType != this.jobDataType)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' for job '{this.JobName}' uses data type '{this.dataType.FullName}' but the job contract expects '{this.jobDataType.FullName}'.");
        }

        if (this.triggerType == JobTriggerType.OneTime && this.dueUtc is null)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires an absolute due time.");
        }

        if ((this.triggerType == JobTriggerType.Delayed || this.triggerType == JobTriggerType.StartupDelay) && this.delay is null)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a delay.");
        }

        if (this.triggerType == JobTriggerType.Calendar && this.calendar is null)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a calendar definition.");
        }

        if (this.triggerType == JobTriggerType.Custom && this.customTriggerProviderType is null)
        {
            throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires a custom trigger provider type.");
        }

        if (this.triggerType == JobTriggerType.Event)
        {
            if (string.IsNullOrWhiteSpace(this.eventSource) || this.eventDataType is null)
            {
                throw new InvalidOperationException($"The trigger '{this.TriggerName}' requires an event source and event payload type.");
            }

            if (!this.jobDataType.IsAssignableFrom(this.eventDataType))
            {
                throw new InvalidOperationException($"The trigger '{this.TriggerName}' accepts event type '{this.eventDataType.FullName}' but the job contract expects '{this.jobDataType.FullName}' or a base type.");
            }
        }

        return new JobTriggerDefinition
        {
            TriggerName = this.TriggerName,
            TriggerType = this.triggerType,
            Enabled = this.enabled,
            Priority = this.priority,
            Timeout = this.timeout,
            RetryPolicy = this.retryPolicy,
            Data = this.data,
            TimeZone = this.timeZone,
            DataType = this.jobDataType,
            MissedOccurrencePolicy = this.missedOccurrencePolicy,
            Properties = new PropertyBag(this.properties.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase)),
            TargetInstances = this.targetInstances.Count == 0 ? null : this.targetInstances.ToArray(),
            Schedule = this.schedule,
            DueUtc = this.dueUtc,
            Delay = this.delay,
            Calendar = this.calendar,
            CustomTriggerProviderType = this.customTriggerProviderType,
            EventSource = this.eventSource,
            EventDataType = this.eventDataType,
        };
    }
}
