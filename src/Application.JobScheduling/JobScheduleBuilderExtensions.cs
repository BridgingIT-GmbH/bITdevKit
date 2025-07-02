// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application;

using BridgingIT.DevKit.Application.JobScheduling;

public static class JobScheduleBuilderExtensions
{
    /// <summary>
    /// Sets the cron expression for the job schedule using a fluent CronExpressionBuilder.
    /// </summary>
    /// <typeparam name="TJob">The job type implementing IJob.</typeparam>
    /// <param name="builder">The JobScheduleBuilder instance.</param>
    /// <param name="cronExpressionBuilder">A function that takes a CronExpressionBuilder and returns the built cron expression string.</param>
    /// <returns>The JobScheduleBuilder instance for fluent chaining.</returns>
    /// <example>
    /// // Schedule a job to run on the 1st of every month at 11:59 PM
    /// services.AddJobScheduling(builder.Configuration)
    ///     .WithJob<EchoJob>()
    ///         .Cron(b => b
    ///             .DayOfMonth(1)
    ///             .AtTime(23, 59, 0)
    ///             .Build())
    ///         .Named("monthlyEcho")
    ///         .RegisterScoped();
    /// </example>
    public static JobScheduleBuilder<TJob> Cron<TJob>(
        this JobScheduleBuilder<TJob> builder,
        Func<CronExpressionBuilder, string> cronExpressionBuilder)
        where TJob : class, IJob
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(cronExpressionBuilder);

        var cronExpression = cronExpressionBuilder(new CronExpressionBuilder());
        if (string.IsNullOrEmpty(cronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or empty.", nameof(cronExpressionBuilder));
        }

        return builder.SetCronExpression(cronExpression);
    }
}