namespace BridgingIT.DevKit.Presentation.UnitTests.Cli;

using System.Text.Json;
using BridgingIT.DevKit.Cli;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
[Collection(ProcessWideStateTestCollection.Name)]
public sealed class DevCliCommandTests : IDisposable
{
    private readonly FakeGitRepository repository = new();
    private readonly FakeProcessRunner processRunner = new();
    private readonly FakeGitRunner gitRunner = new();
    private readonly List<IDisposable> disposables = [];
    private readonly HostRegistryOptions options;

    public DevCliCommandTests()
    {
        var hosts = Path.Combine(this.repository.Root, "hosts");
        Directory.CreateDirectory(hosts);
        this.options = new HostRegistryOptions { RuntimePath = hosts, SelectionPath = hosts };
    }

    public void Dispose()
    {
        foreach (var disposable in this.disposables)
        {
            disposable.Dispose();
        }

        this.repository.Dispose();
    }

    [Fact]
    public void GetStatus_OrdinaryCheckout_ReportsOrdinaryMode()
    {
        // Arrange
        var sut = this.CreateService();

        // Act
        var result = sut.GetStatus(Workspace(this.repository.MainRoot));

        // Assert
        result.Succeeded.ShouldBeTrue();
        Json(result.Data).GetProperty("mode").GetString().ShouldBe("ordinary");
    }

    [Fact]
    public void GetStatus_AssignedWorktree_ReportsSlotUrlsTokenAndResources()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 3, withResources: true);
        var sut = this.CreateService();

        // Act
        var result = sut.GetStatus(Workspace(worktree));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var data = Json(result.Data);
        data.GetProperty("schemaVersion").GetInt32().ShouldBe(DevAppInstanceCliService.StatusSchemaVersion);
        data.GetProperty("mode").GetString().ShouldBe("managed");
        data.GetProperty("slot").GetInt32().ShouldBe(3);
        data.GetProperty("worktreeId").GetGuid().ShouldBe(worktreeId);
        var app = data.GetProperty("applications")[0];
        app.GetProperty("appId").GetString().ShouldBe("forecasthub");
        app.GetProperty("httpsUrl").GetString().ShouldBe("https://localhost:5311");
        app.GetProperty("instanceToken").GetString().ShouldBe("forecasthub:w03");
        app.GetProperty("process").GetProperty("state").GetString().ShouldBe("stopped");
        app.GetProperty("resources")[0].GetProperty("target").GetString().ShouldBe("ForecastHub_w03_Core");
    }

    [Fact]
    public void ConfigureSlot_OrdinaryCheckout_Refuses()
    {
        // Arrange
        var sut = this.CreateService();

        // Act
        var result = sut.ConfigureSlot(Workspace(this.repository.MainRoot), 4);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe(DevAppInstanceInactiveReasons.OrdinaryCheckout);
    }

    [Fact]
    public void ConfigureSlot_NewWorktree_WritesRegistryAndLocalAssignmentIdempotently()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var sut = this.CreateService();

        // Act
        var first = sut.ConfigureSlot(Workspace(worktree), 7);
        var second = sut.ConfigureSlot(Workspace(worktree), 7);

        // Assert
        first.Succeeded.ShouldBeTrue();
        second.Succeeded.ShouldBeTrue();
        second.Summary.ShouldContain("already assigned");
        var registry = this.LoadRegistry();
        registry.Assignments.Count.ShouldBe(1);
        registry.Assignments[0].Slot.ShouldBe(7);
        DevAppInstanceLocalAssignment.Read(worktree).Slot.ShouldBe(7);
    }

    [Fact]
    public void ConfigureSlot_SlotOwnedByOtherWorktree_ReturnsSlotInUse()
    {
        // Arrange
        this.Assign(this.repository.AddWorktree("feature-a"), 2);
        var worktree = this.repository.AddWorktree("feature-b");
        var sut = this.CreateService();

        // Act
        var result = sut.ConfigureSlot(Workspace(worktree), 2);

        // Assert
        result.Code.ShouldBe(DevAppInstanceErrorCodes.SlotInUse);
    }

    [Fact]
    public void ConfigureSlot_MoveWithRemainingResources_Refuses()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2, withResources: true);
        var sut = this.CreateService();

        // Act
        var result = sut.ConfigureSlot(Workspace(worktree), 5);

        // Assert
        result.Code.ShouldBe("resources_remaining");
        result.Summary.ShouldContain("--confirm-instance forecasthub:w02");
        this.LoadRegistry().Assignments[0].Slot.ShouldBe(2);
    }

    [Fact]
    public void ConfigureSlot_MoveWithoutResources_ChangesSlot()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2);
        var sut = this.CreateService();

        // Act
        var result = sut.ConfigureSlot(Workspace(worktree), 5);

        // Assert
        result.Succeeded.ShouldBeTrue();
        this.LoadRegistry().Assignments[0].Slot.ShouldBe(5);
        DevAppInstanceLocalAssignment.Read(worktree).Slot.ShouldBe(5);
    }

    [Fact]
    public void ForgetApplication_WithResources_RefusesAndWithoutResources_Removes()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 2, withResources: true);
        var sut = this.CreateService();

        // Act
        var refused = sut.ForgetApplication(Workspace(worktree), "forecasthub");
        this.SetInventory(worktreeId, 2, []);
        var removed = sut.ForgetApplication(Workspace(worktree), "forecasthub");

        // Assert
        refused.Code.ShouldBe("resources_remaining");
        removed.Succeeded.ShouldBeTrue();
        this.LoadRegistry().Applications.ShouldBeEmpty();
    }

    [Fact]
    public void Release_WrongSlot_ReturnsInvalidArguments()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2);
        var sut = this.CreateService();

        // Act
        var result = sut.Release(Workspace(worktree), 3);

        // Assert
        result.ExitCode.ShouldBe(CliExitCode.InvalidArguments);
        result.Summary.ShouldContain("--confirm-slot 2");
    }

    [Fact]
    public void Release_HeldLease_ReturnsInstanceRunning()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 2);
        this.HoldLease(worktreeId);
        var sut = this.CreateService();

        // Act
        var result = sut.Release(Workspace(worktree), 2);

        // Assert
        result.Code.ShouldBe(DevAppInstanceErrorCodes.InstanceRunning);
        this.LoadRegistry().Assignments.Count.ShouldBe(1);
    }

    [Fact]
    public void Release_RemainingResources_Refuses()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2, withResources: true);
        var sut = this.CreateService();

        // Act
        var result = sut.Release(Workspace(worktree), 2);

        // Assert
        result.Code.ShouldBe("resources_remaining");
    }

    [Fact]
    public void Release_IdleWorktreeWithoutResources_RemovesAssignmentAndLocalFile()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2);
        var sut = this.CreateService();

        // Act
        var result = sut.Release(Workspace(worktree), 2);

        // Assert
        result.Succeeded.ShouldBeTrue();
        this.LoadRegistry().Assignments.ShouldBeEmpty();
        File.Exists(DevAppInstancePaths.GetAssignmentFile(worktree)).ShouldBeFalse();
    }

    [Fact]
    public async Task ResetAsync_WrongToken_ReturnsInvalidArgumentsWithoutStartingProcess()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2, withResources: true);
        var sut = this.CreateService();

        // Act
        var result = await sut.ResetAsync(Workspace(worktree), "forecasthub", "forecasthub:w03", CancellationToken.None);

        // Assert
        result.ExitCode.ShouldBe(CliExitCode.InvalidArguments);
        result.Summary.ShouldContain("forecasthub:w02");
        this.processRunner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetAsync_HeldLease_ReturnsInstanceRunning()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 2, withResources: true);
        this.HoldLease(worktreeId);
        var sut = this.CreateService();

        // Act
        var result = await sut.ResetAsync(Workspace(worktree), "forecasthub", "forecasthub:w02", CancellationToken.None);

        // Assert
        result.Code.ShouldBe(DevAppInstanceErrorCodes.InstanceRunning);
        this.processRunner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetAsync_ConfirmedToken_StartsProjectWithResetArgumentAndParsesResult()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2, withResources: true);
        Directory.CreateDirectory(Path.Combine(worktree, "src", "ForecastHub"));
        this.processRunner.Result = new DevAppInstanceProcessResult(
            0,
            "info: starting\n[BDK-WORKTREE-RESULT] {\"mode\":\"reset\",\"appId\":\"forecasthub\",\"succeeded\":true,\"completed\":[\"ForecastHub_w02_Core\"],\"remaining\":[],\"errors\":[]}\n",
            string.Empty);
        var sut = this.CreateService();

        // Act
        var result = await sut.ResetAsync(Workspace(worktree), "forecasthub", "forecasthub:w02", CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Lines.ShouldContain(l => l.Contains("dropped:   ForecastHub_w02_Core"));
        var call = this.processRunner.Calls.ShouldHaveSingleItem();
        call.FileName.ShouldBe("dotnet");
        call.Arguments.ShouldContain("--no-launch-profile");
        call.Arguments.ShouldContain("--bdk-dev-reset=forecasthub:w02");
        call.Arguments.ShouldContain(Path.Combine(worktree, "src", "ForecastHub"));
    }

    [Theory]
    [InlineData(2, "{\"succeeded\":false,\"errorCode\":\"resource_not_owned\",\"completed\":[],\"remaining\":[\"X\"],\"errors\":[\"not owned\"]}", "resource_not_owned")]
    [InlineData(3, "{\"succeeded\":false,\"errorCode\":\"reset_incomplete\",\"completed\":[\"A\"],\"remaining\":[\"B\"],\"errors\":[\"locked\"]}", "reset_incomplete")]
    public void ToOutcome_FailedResult_MapsExitCodeAndErrorCode(int exitCode, string json, string expectedCode)
    {
        // Arrange
        var process = new DevAppInstanceProcessResult(exitCode, $"[BDK-WORKTREE-RESULT] {json}", string.Empty);

        // Act
        var result = DevAppInstanceCliService.ToOutcome("reset", "forecasthub", process, []);

        // Assert
        ((int)result.ExitCode).ShouldBe(exitCode);
        result.Code.ShouldBe(expectedCode);
        result.Lines.ShouldContain(l => l.StartsWith("remaining:"));
    }

    [Fact]
    public void ToOutcome_MissingResultLine_ReturnsNoResultWithOutputTail()
    {
        // Arrange
        var process = new DevAppInstanceProcessResult(1, "build output", "error CS1002: ; expected");

        // Act
        var result = DevAppInstanceCliService.ToOutcome("reset", "forecasthub", process, []);

        // Assert
        result.Code.ShouldBe("reset_no_result");
        result.Lines.ShouldContain("error CS1002: ; expected");
    }

    [Fact]
    public void ParseWorktreeList_Porcelain_ReturnsWorktreePaths()
    {
        // Arrange
        const string porcelain = "worktree F:/repo\nHEAD abc\nbranch refs/heads/main\n\nworktree C:/wt/feature-a\nHEAD def\ndetached\n";

        // Act
        var result = DevAppInstanceCliService.ParseWorktreeList(porcelain);

        // Assert
        result.ShouldBe(["F:/repo", "C:/wt/feature-a"]);
    }

    [Fact]
    public async Task PruneAsync_WithoutConfirm_ListsOrphanWithoutChanges()
    {
        // Arrange
        var orphan = this.repository.AddWorktree("deleted");
        this.Assign(orphan, 4, withResources: true);
        Directory.Delete(orphan, true);
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: false, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Summary.ShouldContain("1 orphaned");
        result.Lines.ShouldContain(l => l.Contains("ForecastHub_w04_Core"));
        this.processRunner.Calls.ShouldBeEmpty();
        this.LoadRegistry().Assignments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PruneAsync_Confirm_RunsOrphanResetFromCurrentCheckoutAndReleasesSlot()
    {
        // Arrange
        var orphan = this.repository.AddWorktree("deleted");
        var worktreeId = this.Assign(orphan, 4, withResources: true);
        Directory.Delete(orphan, true);
        Directory.CreateDirectory(Path.Combine(this.repository.MainRoot, "src", "ForecastHub"));
        this.processRunner.Result = new DevAppInstanceProcessResult(0, "[BDK-WORKTREE-RESULT] {\"succeeded\":true,\"completed\":[\"ForecastHub_w04_Core\"],\"remaining\":[],\"errors\":[]}", string.Empty);
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: true, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        this.processRunner.Calls.ShouldHaveSingleItem().Arguments.ShouldContain($"--bdk-dev-orphan-reset={worktreeId:D}");
        this.LoadRegistry().Assignments.ShouldBeEmpty();
    }

    [Fact]
    public async Task PruneAsync_ConfirmWithMissingProject_KeepsAssignmentAndNamesCatalogs()
    {
        // Arrange
        var orphan = this.repository.AddWorktree("deleted");
        this.Assign(orphan, 4, withResources: true);
        Directory.Delete(orphan, true);
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: true, CancellationToken.None);

        // Assert
        result.Code.ShouldBe(DevAppInstanceErrorCodes.ResetIncomplete);
        result.Lines.ShouldContain(l => l.Contains("drop the databases manually"));
        this.LoadRegistry().Assignments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PruneAsync_PathMissingFromGitWorktreeList_IsOrphan()
    {
        // Arrange
        var stale = this.repository.AddWorktree("pruned");
        this.Assign(stale, 4);
        this.gitRunner.Output = $"worktree {this.repository.MainRoot}\nHEAD abc\n";
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: false, CancellationToken.None);

        // Assert
        result.Summary.ShouldContain("1 orphaned");
    }

    [Fact]
    public async Task PruneAsync_WorktreeMovedByGit_IsNotOrphan()
    {
        // Arrange
        var original = this.repository.AddWorktree("moved-from");
        var worktreeId = this.Assign(original, 4, withResources: true);
        var moved = Path.Combine(Path.GetDirectoryName(original), "moved-to");
        Directory.Move(original, moved);
        this.gitRunner.Output = $"worktree {this.repository.MainRoot}\nHEAD abc\n\nworktree {moved}\nHEAD def\n";
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: true, CancellationToken.None);

        // Assert
        result.Summary.ShouldBe("No orphaned assignments.");
        this.processRunner.Calls.ShouldBeEmpty();
        this.LoadRegistry().Assignments.ShouldContain(a => a.WorktreeId == worktreeId);
    }

    [Fact]
    public async Task PruneAsync_LockedWorktreeWithMissingDirectory_IsNotOrphan()
    {
        // Arrange
        var unplugged = this.repository.AddWorktree("removable");
        this.Assign(unplugged, 4, withResources: true);
        Directory.Delete(unplugged, true);
        this.gitRunner.Output = $"worktree {this.repository.MainRoot}\nHEAD abc\n\nworktree {unplugged}\nHEAD def\nlocked on usb drive\n";
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: false, CancellationToken.None);

        // Assert
        result.Summary.ShouldBe("No orphaned assignments.");
    }

    [Fact]
    public async Task PruneAsync_PrunableWorktreeEntry_IsOrphan()
    {
        // Arrange
        var deleted = this.repository.AddWorktree("deleted");
        this.Assign(deleted, 4, withResources: true);
        Directory.Delete(deleted, true);
        this.gitRunner.Output = $"worktree {this.repository.MainRoot}\nHEAD abc\n\nworktree {deleted}\nHEAD def\nprunable gitdir file points to non-existent location\n";
        var sut = this.CreateService();

        // Act
        var result = await sut.PruneAsync(Workspace(this.repository.MainRoot), confirm: false, CancellationToken.None);

        // Assert
        result.Summary.ShouldContain("1 orphaned");
    }

    [Fact]
    public async Task StopAsync_NotRunning_ReturnsHostNotFound()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2);
        var sut = this.CreateService();

        // Act
        var result = await sut.StopAsync(Workspace(worktree), "forecasthub", force: false, CancellationToken.None);

        // Assert
        result.ExitCode.ShouldBe(CliExitCode.HostNotFound);
    }

    [Fact]
    public async Task StopAsync_HeldLeaseReleasedAfterStopRequest_ReturnsStopped()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 2);
        var leasePath = DevAppInstancePaths.GetLeaseFile(this.repository.CommonDirectory, worktreeId, "forecasthub");
        var lease = this.HoldLease(worktreeId);
        var stopFile = DevAppInstanceLease.GetStopRequestFile(leasePath);
        _ = Task.Run(async () =>
        {
            while (!File.Exists(stopFile))
            {
                await Task.Delay(50);
            }

            File.Delete(stopFile);
            lease.Dispose();
        });
        var sut = this.CreateService();

        // Act
        var result = await sut.StopAsync(Workspace(worktree), "forecasthub", force: false, CancellationToken.None, TimeSpan.FromSeconds(10));

        // Assert
        result.Succeeded.ShouldBeTrue();
        Json(result.Data).GetProperty("forced").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task StopAsync_Timeout_ReportsTimeoutAndRemovesStopRequest()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 2);
        this.HoldLease(worktreeId);
        var leasePath = DevAppInstancePaths.GetLeaseFile(this.repository.CommonDirectory, worktreeId, "forecasthub");
        var sut = this.CreateService();

        // Act
        var result = await sut.StopAsync(Workspace(worktree), "forecasthub", force: false, CancellationToken.None, TimeSpan.FromMilliseconds(300));

        // Assert
        result.Code.ShouldBe("stop_timeout");
        File.Exists(DevAppInstanceLease.GetStopRequestFile(leasePath)).ShouldBeFalse();
    }

    [Fact]
    public void LeaseHelpers_HeldLeaseWithHolderInfo_ReportHeldAndHolder()
    {
        // Arrange
        var worktreeId = Guid.NewGuid();
        var leasePath = DevAppInstancePaths.GetLeaseFile(this.repository.CommonDirectory, worktreeId, "forecasthub");

        // Act
        var before = DevAppInstanceLease.IsHeld(leasePath);
        this.HoldLease(worktreeId);
        var held = DevAppInstanceLease.IsHeld(leasePath);
        var holder = DevAppInstanceLease.ReadHolder(leasePath);

        // Assert
        before.ShouldBeFalse();
        held.ShouldBeTrue();
        holder.ProcessId.ShouldBe(Environment.ProcessId);
        holder.IsSameProcessRunning().ShouldBeTrue();
        new DevAppInstanceLeaseHolder(holder.ProcessId, holder.ProcessStartedAt?.AddMinutes(-5), null, null).IsSameProcessRunning().ShouldBeFalse();
    }

    [Fact]
    public async Task DevConfigureCliCommand_WithoutOptions_ReturnsInvalidArguments()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var state = new CliExecutionState();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(Workspace(this.repository.MainRoot), new CliOutputSettings { Format = CliOutputFormat.Json }, this.options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(state)
            .AddSingleton(this.CreateService())
            .BuildServiceProvider();
        var sut = new DevConfigureCliCommand();

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            state.ExitCode.ShouldBe(CliExitCode.InvalidArguments);
            writer.ToString().ShouldContain("invalid_arguments");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task DevStatusCliCommand_Json_WritesEnvelopeWithData()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var writer = new StringWriter();
        Console.SetOut(writer);
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 2);
        var state = new CliExecutionState();
        var services = new ServiceCollection()
            .AddSingleton(new CliRuntimeContext(Workspace(worktree), new CliOutputSettings { Format = CliOutputFormat.Json }, this.options))
            .AddSingleton(new CliConsole(new CliOutputSettings { Format = CliOutputFormat.Json }))
            .AddSingleton(state)
            .AddSingleton(this.CreateService())
            .BuildServiceProvider();
        var sut = new DevStatusCliCommand();

        try
        {
            // Act
            await sut.ExecuteAsync(Spectre.Console.AnsiConsole.Console, services);

            // Assert
            state.ExitCode.ShouldBe(CliExitCode.Success);
            using var document = JsonDocument.Parse(writer.ToString());
            document.RootElement.GetProperty("succeeded").GetBoolean().ShouldBeTrue();
            document.RootElement.GetProperty("data").GetProperty("slot").GetInt32().ShouldBe(2);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_NoApp_BuildsThenStartsAllDevKitHosts()
    {
        // Arrange
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Presentation.Web.Server");
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastAdmin.Web");
        WriteProject(this.repository.MainRoot, "src/Contoso.Plain.Web", devKit: false);
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Presentation.Web.Server/bin/Debug/Copy");
        var output = new List<string>();
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), [], false, output.Add, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue(result.Summary);
        this.processRunner.Calls.Count.ShouldBe(4);
        this.processRunner.Calls.Take(2).ShouldAllBe(c => c.Arguments[0] == "build");
        this.processRunner.Calls.Skip(2).ShouldAllBe(c => c.Arguments[0] == "run" && c.Arguments.Contains("--no-build"));
        this.processRunner.Calls.ShouldNotContain(c => c.Arguments.Any(a => a.Contains("Plain")) || c.Arguments.Any(a => a.Contains("Copy")));
        output.ShouldContain("[forecastadmin] Now listening on: https://localhost:5311");
        output.ShouldContain("[forecasthub  ] Now listening on: https://localhost:5311");
        Json(result.Data).GetProperty("hosts").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_AppList_StartsOnlySelectedHosts()
    {
        // Arrange
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Web");
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastAdmin.Web");
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), ["forecasthub"], true, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue(result.Summary);
        this.processRunner.Calls.Count.ShouldBe(1);
        this.processRunner.Calls[0].Arguments.ShouldContain(a => a.EndsWith("Contoso.ForecastHub.Web.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_UnknownApp_ReturnsAppNotFoundWithAvailableIds()
    {
        // Arrange
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Web");
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), ["forecasthub", "missing"], false, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("app_not_found");
        result.ExitCode.ShouldBe(CliExitCode.InvalidArguments);
        result.Summary.ShouldContain("missing");
        result.Summary.ShouldContain("Available: forecasthub");
        this.processRunner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_RecordedInventory_UsesRegisteredAppId()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        this.Assign(worktree, 3);
        WriteProject(worktree, "src/ForecastHub", assemblyName: "Contoso.Portal.Web");
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(worktree), ["forecasthub"], true, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue(result.Summary);
        Json(result.Data).GetProperty("slot").GetInt32().ShouldBe(3);
        this.processRunner.Calls.Single().Arguments.ShouldContain(a => a.EndsWith("ForecastHub.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_InstanceAlreadyRunning_SkipsHost()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("feature-a");
        var worktreeId = this.Assign(worktree, 3);
        WriteProject(worktree, "src/ForecastHub");
        this.HoldLease(worktreeId);
        var output = new List<string>();
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(worktree), [], false, output.Add, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Summary.ShouldContain("already running");
        output.ShouldContain(l => l.StartsWith("[forecasthub] already running", StringComparison.Ordinal));
        this.processRunner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_BuildFails_StartsNoHost()
    {
        // Arrange
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Web");
        this.processRunner.Result = new DevAppInstanceProcessResult(1, "error CS1002: ; expected", string.Empty);
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), [], false, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("build_failed");
        result.Lines.ShouldContain("error CS1002: ; expected");
        this.processRunner.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_HostExitsWithError_ReturnsHostFailed()
    {
        // Arrange
        WriteProject(this.repository.MainRoot, "src/Contoso.ForecastHub.Web");
        this.processRunner.StreamingExitCode = 3;
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), [], true, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("host_failed");
        result.Summary.ShouldContain("forecasthub");
    }

    [Fact]
    public async Task RunAsync_NoHostProject_ReturnsNoHostProject()
    {
        // Arrange
        var sut = this.CreateService();

        // Act
        var result = await sut.RunAsync(Workspace(this.repository.MainRoot), [], false, null, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("no_host_project");
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("forecasthub", new[] { "forecasthub" })]
    [InlineData(" forecasthub , forecastadmin ,", new[] { "forecasthub", "forecastadmin" })]
    public void ParseAppIds_CommaSeparatedValue_ReturnsTrimmedIds(string value, string[] expected)
    {
        // Act
        var result = DevRunCliCommand.ParseAppIds(value);

        // Assert
        result.ShouldBe(expected);
    }

    private static void WriteProject(string root, string relativeDirectory, bool devKit = true, string assemblyName = null)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        var name = Path.GetFileName(directory);
        File.WriteAllText(
            Path.Combine(directory, $"{name}.csproj"),
            $"<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup>{(assemblyName is null ? string.Empty : $"<AssemblyName>{assemblyName}</AssemblyName>")}</PropertyGroup></Project>");
        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            devKit ? "var builder = DevKitWebApplication.CreateBuilder(args);" : "var builder = WebApplication.CreateBuilder(args);");
    }

    private DevAppInstanceCliService CreateService() =>
        new(new HostRuntimeDiscovery(this.options), new FakeHostProcessManager(), this.processRunner, this.gitRunner);

    private static CliWorkspaceContext Workspace(string path) => new WorkspaceResolver().Resolve(path);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private string RegistryPath => DevAppInstancePaths.GetRegistryFile(this.repository.CommonDirectory);

    private DevAppInstanceRegistry LoadRegistry() => DevAppInstanceRegistry.Load(this.RegistryPath);

    private Guid Assign(string worktree, int slot, bool withResources = false)
    {
        var registry = this.LoadRegistry();
        if (registry.Applications.Count == 0)
        {
            registry.AddApplication(new DevAppInstanceApplication("forecasthub", httpOffset: 10, httpsOffset: 11));
        }

        var worktreeId = Guid.NewGuid();
        registry.AddAssignment(worktreeId, slot, GitCheckoutDetector.Canonicalize(worktree), DateTimeOffset.UtcNow);
        registry.Save(this.RegistryPath);
        new DevAppInstanceLocalAssignment(worktreeId, slot).Write(worktree);
        this.SetInventory(
            worktreeId,
            slot,
            withResources ? [new DevAppInstanceInventoryResource("Core", "sqlserver", $"ForecastHub_w{slot:00}_Core", "localhost,14333", ["ConnectionStrings:Core"], false)] : []);

        return worktreeId;
    }

    private void SetInventory(Guid worktreeId, int slot, IReadOnlyList<DevAppInstanceInventoryResource> resources)
    {
        var registry = this.LoadRegistry();
        registry.SetInventory(new DevAppInstanceInventory("forecasthub", worktreeId, slot, "src/ForecastHub", resources));
        registry.Save(this.RegistryPath);
    }

    private FileStream HoldLease(Guid worktreeId)
    {
        var leasePath = DevAppInstancePaths.GetLeaseFile(this.repository.CommonDirectory, worktreeId, "forecasthub");
        Directory.CreateDirectory(Path.GetDirectoryName(leasePath));
        File.WriteAllText(
            leasePath + ".json",
            JsonSerializer.Serialize(new { processId = Environment.ProcessId, processStartedAt = DevAppInstanceLease.GetCurrentProcessStartedAt()?.ToString("O") }));
        var stream = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        this.disposables.Add(stream);

        return stream;
    }

    private sealed class FakeProcessRunner : IDevAppInstanceProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)> Calls { get; } = [];

        public DevAppInstanceProcessResult Result { get; set; } = new(0, string.Empty, string.Empty);

        public Task<DevAppInstanceProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
        {
            this.Calls.Add((fileName, arguments, workingDirectory));

            return Task.FromResult(this.Result);
        }

        public int StreamingExitCode { get; set; }

        public IReadOnlyList<string> StreamingOutput { get; set; } = ["Now listening on: https://localhost:5311"];

        public Task<int> RunStreamingAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, Action<string> onOutput, CancellationToken stopToken, CancellationToken killToken)
        {
            lock (this.Calls)
            {
                this.Calls.Add((fileName, arguments, workingDirectory));
            }

            foreach (var line in this.StreamingOutput)
            {
                onOutput(line);
            }

            return Task.FromResult(this.StreamingExitCode);
        }
    }

    private sealed class FakeGitRunner : IGitCommandRunner
    {
        public string Output { get; set; }

        public string Run(string workingDirectory, params string[] arguments) =>
            arguments.Length > 0 && arguments[0] == "worktree" ? this.Output : null;
    }

    private sealed class FakeHostProcessManager : IHostProcessManager
    {
        public List<int> KilledProcessIds { get; } = [];

        public HostProcessKillResult Kill(int processId)
        {
            this.KilledProcessIds.Add(processId);

            return new HostProcessKillResult(true);
        }
    }
}
