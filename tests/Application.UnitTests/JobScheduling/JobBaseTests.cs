// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.JobScheduling;

using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

/// <summary>Verifies repeated Quartz execution with retained trigger metadata.</summary>
/// <example>dotnet test --filter FullyQualifiedName~JobBaseTests</example>
public class JobBaseTests
{
    /// <summary>Successful execution leaves nullable metadata which must not prevent the next execution.</summary>
    /// <example>await test.Execute_AfterSuccess_PreservesRepeatedExecutionWithNullMetadata();</example>
    [Fact]
    public async Task Execute_AfterSuccess_PreservesRepeatedExecutionWithNullMetadata()
    {
        var job = new RecordingJob();
        var detail = JobBuilder.Create<RecordingJob>().WithIdentity("repeat")
            .UsingJobData("JobId", "repeat-id").UsingJobData("input", "value").Build();
        var trigger = TriggerBuilder.Create().WithIdentity("repeat-trigger").ForJob(detail).StartNow().Build();
        var context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Returns(detail);
        context.Trigger.Returns(trigger);
        context.CancellationToken.Returns(CancellationToken.None);
        context.MergedJobDataMap.Returns(new JobDataMap((IDictionary<string, object>)detail.JobDataMap));

        await job.Execute(context);

        trigger.JobDataMap[nameof(JobBase.ErrorMessage)].ShouldBeNull();
        var nextData = new JobDataMap((IDictionary<string, object>)detail.JobDataMap);
        foreach (var field in trigger.JobDataMap)
        {
            nextData[field.Key] = field.Value;
        }

        context.MergedJobDataMap.Returns(nextData);

        await job.Execute(context);

        job.Executions.ShouldBe(2);
        job.Data["input"].ShouldBe("value");
        job.Data[nameof(JobBase.ErrorMessage)].ShouldBe(string.Empty);
        job.Status.ShouldBe(JobStatus.Success);
    }

    private sealed class RecordingJob() : JobBase(NullLoggerFactory.Instance)
    {
        /// <summary>Gets the number of business executions.</summary>
        public int Executions { get; private set; }

        /// <inheritdoc/>
        public override Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            this.Executions++;
            return Task.CompletedTask;
        }
    }
}
