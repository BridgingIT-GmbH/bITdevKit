// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Characterizes configuration overrides over existing code-owned job definitions.</summary>
public sealed class JobConfigurationCompositionTests
{
    private const string CodeSchedule = "0 0 2 * * *";

    /// <summary>Schedule takes precedence over its Cron alias, including an explicit blank which keeps the code schedule.</summary>
    [Theory]
    [InlineData(null, "0 5 1 * * *", "0 5 1 * * *")]
    [InlineData("0 10 1 * * *", "0 5 1 * * *", "0 10 1 * * *")]
    [InlineData("", "0 5 1 * * *", CodeSchedule)]
    [InlineData("   ", "0 5 1 * * *", CodeSchedule)]
    public void GetDefinitions_ScheduleAndCronAliases_PreserveExistingPrecedence(string schedule, string cron, string expected)
    {
        var sut = CreateStore(new()
        {
            ["Jobs:cleanup:Triggers:nightly:Schedule"] = schedule,
            ["Jobs:cleanup:Triggers:nightly:Cron"] = cron,
        });

        var resolved = sut.GetDefinitions().Single();

        resolved.Triggers.Single().Schedule.ShouldBe(expected);
    }

    /// <summary>Blank and malformed booleans do not override either the job or trigger's code-owned enabled state.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("0")]
    public void GetDefinitions_UnparsedEnabledValues_RetainJobAndTriggerStates(string enabled)
    {
        var sut = CreateStore(new()
        {
            ["Jobs:cleanup:Enabled"] = enabled,
            ["Jobs:cleanup:Triggers:nightly:Enabled"] = enabled,
        }, CreateDefinition() with { Enabled = false });

        var resolved = sut.GetDefinitions().Single();

        resolved.Enabled.ShouldBeFalse();
        resolved.Triggers.Single().Enabled.ShouldBeTrue();
    }

    /// <summary>Empty configured targets inherit the existing nonempty job and trigger target lists.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ; ")]
    public void GetDefinitions_EmptyTargetOverrides_RetainCodeTargets(string targets)
    {
        var sut = CreateStore(new()
        {
            ["Jobs:cleanup:Enabled"] = "true",
            ["Jobs:cleanup:TargetInstances"] = targets,
            ["Jobs:cleanup:Triggers:nightly:Enabled"] = "true",
            ["Jobs:cleanup:Triggers:nightly:TargetInstances"] = targets,
        });

        var resolved = sut.GetDefinitions().Single();

        resolved.TargetInstances.ShouldBe(["code-node"]);
        resolved.Triggers.Single().TargetInstances.ShouldBe(["trigger-node"]);
    }

    /// <summary>Indexed arrays trim and discard empty targets; a nonblank inline value takes precedence over indexed children.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetDefinitions_TargetRepresentations_PreserveParsingAndReplacement(bool inline)
    {
        var settings = new Dictionary<string, string>();
        foreach (var prefix in new[] { "Jobs:cleanup", "Jobs:cleanup:Triggers:nightly" })
        {
            settings[prefix + ":TargetInstances:0"] = " node-a ";
            settings[prefix + ":TargetInstances:1"] = " ";
            settings[prefix + ":TargetInstances:2"] = "node-b";
            if (inline) { settings[prefix + ":TargetInstances"] = " node-c; node-d, "; }
        }

        var sut = CreateStore(settings);

        var resolved = sut.GetDefinitions().Single();

        var expected = inline ? new[] { "node-c", "node-d" } : ["node-a", "node-b"];
        resolved.TargetInstances.ShouldBe(expected);
        resolved.Triggers.Single().TargetInstances.ShouldBe(expected);
    }

    /// <summary>Resolution preserves exact configured strings, including unpaired UTF-16 code units.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetDefinitions_UnpairedUtf16InConfiguration_PreservesExactScheduleAndTargets(bool indexedTargets)
    {
        var unpaired = new string((char)0xd800, 1);
        var schedule = CodeSchedule + unpaired;
        var target = "node-" + unpaired;
        var settings = new Dictionary<string, string>
        {
            ["Jobs:cleanup:Triggers:nightly:Schedule"] = schedule,
        };
        foreach (var prefix in new[] { "Jobs:cleanup", "Jobs:cleanup:Triggers:nightly" })
        {
            settings[prefix + (indexedTargets ? ":TargetInstances:0" : ":TargetInstances")] = target;
        }

        var sut = CreateStore(settings);

        var resolved = sut.GetDefinitions().Single();

        resolved.TargetInstances.Single().ShouldBe(target);
        resolved.Triggers.Single().TargetInstances.Single().ShouldBe(target);
        resolved.Triggers.Single().Schedule.ShouldBe(schedule);
    }

    /// <summary>Overlays preserve runtime objects and types while copying property bags and leaving registered definitions unchanged.</summary>
    [Fact]
    public void GetDefinitions_RuntimeTypesDelegatesAndProperties_PreserveIdentityAndOriginalDefinition()
    {
        var resource = new object();
        Func<string> callback = () => "live";
        var code = CreateDefinition();
        code = code with
        {
            Properties = new() { ["resource"] = resource, ["callback"] = callback },
            Triggers = [code.Triggers.Single() with
            {
                Data = resource,
                Properties = new() { ["callback"] = callback },
            }],
        };
        var sut = CreateStore(new()
        {
            ["Jobs:cleanup:Enabled"] = "false",
            ["Jobs:cleanup:Triggers:nightly:Enabled"] = "false",
            ["Jobs:cleanup:Triggers:nightly:Schedule"] = "0 15 1 * * *",
        }, code);

        var resolved = sut.GetDefinitions().Single();

        resolved.JobType.ShouldBeSameAs(code.JobType);
        resolved.DataType.ShouldBeSameAs(code.DataType);
        resolved.Properties.ShouldNotBeSameAs(code.Properties);
        resolved.Properties["resource"].ShouldBeSameAs(resource);
        resolved.Properties["callback"].ShouldBeSameAs(callback);
        resolved.Triggers.Single().Data.ShouldBeSameAs(resource);
        resolved.Triggers.Single().Properties.ShouldNotBeSameAs(code.Triggers.Single().Properties);
        resolved.Triggers.Single().Properties["callback"].ShouldBeSameAs(callback);
        code.Enabled.ShouldBeTrue();
        code.Triggers.Single().Enabled.ShouldBeTrue();
        code.Triggers.Single().Schedule.ShouldBe(CodeSchedule);
        resolved.Properties["added"] = "snapshot-only";
        code.Properties.Get("added").ShouldBeNull();
    }

    /// <summary>Inline handlers remain executable after configuration changes the associated job and trigger metadata.</summary>
    [Fact]
    public async Task GetDefinitions_InlineExecutionDelegate_RemainsRegisteredAfterOverrides()
    {
        var configuration = Configuration(new()
        {
            ["Jobs:inline:TargetInstances"] = "configured-node",
            ["Jobs:inline:Triggers:manual:Enabled"] = "false",
        });
        var services = new ServiceCollection();
        services.AddLogging();
        var calls = 0;
        services.AddJobScheduler(configuration).AliveEnabled(false)
            .WithJob("inline", job => job
                .Execute((_, _) =>
                {
                    calls++;
                    return Task.FromResult(Result.Success());
                })
                .AddTrigger("manual", trigger => trigger.Manual()));
        using var provider = services.BuildServiceProvider();
        var definition = provider.GetRequiredService<JobRegistrationStore>().GetDefinitions().Single();
        var sut = (IJob)provider.GetRequiredService(definition.JobType);

        var result = await sut.ExecuteAsync(new JobExecutionContextBuilder<Unit>().WithJobName("inline").Build());

        result.IsSuccess.ShouldBeTrue();
        calls.ShouldBe(1);
        definition.TargetInstances.ShouldBe(["configured-node"]);
        definition.Triggers.Single().Enabled.ShouldBeFalse();
    }

    private static JobRegistrationStore CreateStore(Dictionary<string, string> settings, JobDefinition definition = null)
    {
        var store = new JobRegistrationStore();
        store.Add(definition ?? CreateDefinition());
        store.SetConfiguration(Configuration(settings));
        return store;
    }

    private static IConfigurationRoot Configuration(Dictionary<string, string> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static JobDefinition CreateDefinition() => new()
    {
        JobName = "cleanup",
        JobType = typeof(CompatibilityJob),
        DataType = typeof(Unit),
        TargetInstances = ["code-node"],
        Triggers = [new()
        {
            TriggerName = "nightly",
            TriggerType = JobTriggerType.Cron,
            Schedule = CodeSchedule,
            TargetInstances = ["trigger-node"],
        }],
    };

    private sealed class CompatibilityJob : JobBase
    {
        public override Task<Result> ExecuteAsync(IJobExecutionContext<Unit> context, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}