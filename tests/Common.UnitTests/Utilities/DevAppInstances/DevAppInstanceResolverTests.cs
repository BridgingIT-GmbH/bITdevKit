// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;

public class DevAppInstanceResolverTests : IDisposable
{
    private readonly FakeGitRepository repository = new();

    public void Dispose() => this.repository.Dispose();

    [Fact]
    public void Detect_OrdinaryCheckout_ReturnsOrdinary()
    {
        // Arrange & Act
        var result = GitCheckoutDetector.Detect(this.repository.MainRoot);

        // Assert
        result.Kind.ShouldBe(GitCheckoutKind.Ordinary);
        result.IsLinkedWorktree.ShouldBeFalse();
    }

    [Fact]
    public void Detect_LinkedWorktreeSubdirectory_ReturnsLinkedWithCommonDirectory()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var project = Directory.CreateDirectory(Path.Combine(worktree, "src", "App")).FullName;

        // Act
        var result = GitCheckoutDetector.Detect(project);

        // Assert
        result.Kind.ShouldBe(GitCheckoutKind.LinkedWorktree);
        GitCheckoutDetector.PathsEqual(result.WorktreeRoot, worktree).ShouldBeTrue();
        GitCheckoutDetector.PathsEqual(result.CommonDirectory, this.repository.CommonDirectory).ShouldBeTrue();
    }

    [Fact]
    public void Detect_BrokenBackPointerWithoutRunner_ThrowsDetectionFailed()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        File.WriteAllText(Path.Combine(this.repository.CommonDirectory, "worktrees", "wt1", "gitdir"), Path.Combine(this.repository.Root, "elsewhere", ".git"));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => GitCheckoutDetector.Detect(worktree))
            .Code.ShouldBe(DevAppInstanceErrorCodes.WorktreeDetectionFailed);
    }

    [Fact]
    public void Detect_NoRepository_ReturnsOutsideWithoutRunningGit()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(this.repository.Root, "plain", "src")).FullName;
        var runner = new RecordingGitRunner(null);

        // Act
        var result = GitCheckoutDetector.Detect(folder, runner);

        // Assert
        result.ShouldBe(GitCheckoutInfo.Outside);
        result.Warning.ShouldBeNull();
        runner.Calls.ShouldBe(0);
    }

    [Fact]
    public void Detect_HealthyCheckouts_NeverRunGit()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var runner = new RecordingGitRunner(null);

        // Act
        var ordinary = GitCheckoutDetector.Detect(this.repository.MainRoot, runner);
        var linked = GitCheckoutDetector.Detect(worktree, runner);

        // Assert
        ordinary.Kind.ShouldBe(GitCheckoutKind.Ordinary);
        linked.Kind.ShouldBe(GitCheckoutKind.LinkedWorktree);
        runner.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("not a git file")]
    [InlineData("gitdir: ../missing-repo/.git")]
    public void Detect_UnusableGitFileWithoutGit_ReturnsOutsideWithWarning(string content)
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(this.repository.Root, "stray")).FullName;
        File.WriteAllText(Path.Combine(folder, ".git"), content);
        var runner = new RecordingGitRunner(null);

        // Act
        var result = GitCheckoutDetector.Detect(folder, runner);

        // Assert
        result.Kind.ShouldBe(GitCheckoutKind.OutsideGit);
        result.Warning.ShouldContain("Git is not installed");
        runner.Calls.ShouldBe(1);
    }

    [Fact]
    public void Detect_UnusableGitFileWithoutRunner_ReturnsOutsideWithWarning()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(this.repository.Root, "stray")).FullName;
        File.WriteAllText(Path.Combine(folder, ".git"), string.Empty);

        // Act
        var result = GitCheckoutDetector.Detect(folder);

        // Assert
        result.Kind.ShouldBe(GitCheckoutKind.OutsideGit);
        result.Warning.ShouldContain("no 'gitdir:' line");
    }

    [Fact]
    public void Detect_MissingWorktreeMetadataWithoutGit_FailsClosedAfterOneGitCall()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        Directory.Delete(Path.Combine(this.repository.CommonDirectory, "worktrees", "wt1"), true);
        var runner = new RecordingGitRunner(null);

        // Act
        var exception = Should.Throw<DevAppInstanceException>(() => GitCheckoutDetector.Detect(worktree, runner));

        // Assert
        exception.Code.ShouldBe(DevAppInstanceErrorCodes.WorktreeDetectionFailed);
        exception.Message.ShouldContain("Git is not installed");
        exception.RecoveryAction.ShouldContain("DevAppInstance__Mode=Disabled");
        runner.Calls.ShouldBe(1);
    }

    [Fact]
    public void Resolve_NoRepository_ReturnsOutsideGit()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(this.repository.Root, "plain")).FullName;

        // Act
        var result = DevAppInstanceResolver.Resolve(this.repository.Request(folder));

        // Assert
        result.IsActive.ShouldBeFalse();
        result.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OutsideGit);
        result.LogSteps.ShouldNotContain(s => s.Template.StartsWith("[warning] "));
    }

    [Fact]
    public void Resolve_UnusableGitFile_ReturnsOutsideGitWithWarning()
    {
        // Arrange
        var folder = Directory.CreateDirectory(Path.Combine(this.repository.Root, "stray")).FullName;
        File.WriteAllText(Path.Combine(folder, ".git"), "not a git file");

        // Act
        var result = DevAppInstanceResolver.Resolve(this.repository.Request(folder));

        // Assert
        result.IsActive.ShouldBeFalse();
        result.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OutsideGit);
        result.LogSteps.ShouldContain(s => s.Template.StartsWith("[warning] ") && s.Template.Contains("{GitWarning}"));
    }

    [Theory]
    [InlineData("Production", "NONE", DevAppInstanceInactiveReasons.NonDevelopment)]
    [InlineData("Development", "DOTNET_RUNNING_IN_CONTAINER", DevAppInstanceInactiveReasons.Container)]
    [InlineData("Development", "WEBSITE_SITE_NAME", DevAppInstanceInactiveReasons.Cloud)]
    public void Evaluate_IneligibleHost_ReturnsReason(string environment, string variable, string expected)
    {
        // Act
        var result = DevAppInstanceEligibility.Evaluate(
            DevAppInstanceMode.Auto, environment, name => name == variable ? "true" : null, isBuildTime: false);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_OrdinaryCheckout_ReturnsInactiveWithoutFiles()
    {
        // Act
        var sut = DevAppInstanceResolver.Resolve(this.repository.Request(this.repository.MainRoot));

        // Assert
        sut.IsActive.ShouldBeFalse();
        sut.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OrdinaryCheckout);
        Directory.Exists(Path.Combine(this.repository.CommonDirectory, "bdk-dev")).ShouldBeFalse();
    }

    [Fact]
    public void Resolve_DisabledMode_ReturnsInactive()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var request = this.repository.Request(worktree, settings: new Dictionary<string, string> { [DevAppInstanceResolver.ModeKey] = "Disabled" });

        // Act
        var sut = DevAppInstanceResolver.Resolve(request);

        // Assert
        sut.IsActive.ShouldBeFalse();
        sut.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Disabled);
    }

    [Fact]
    public void Resolve_NewWorktree_AssignsLowestSlotAndPersists()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var request = this.repository.Request(worktree, settings: new Dictionary<string, string> { ["Authentication:Authority"] = "https://localhost:5001" });

        // Act
        var sut = DevAppInstanceResolver.Resolve(request);

        // Assert
        sut.IsActive.ShouldBeTrue();
        sut.Slot.ShouldBe(1);
        sut.SelectionReason.ShouldBe(DevAppInstanceSelectionReason.New);
        sut.HttpPort.ShouldBe(5110);
        sut.HttpsPort.ShouldBe(5111);
        sut.ManagedValues["urls"].ShouldBe("https://localhost:5111;http://localhost:5110");
        sut.ManagedValues["Authentication:Authority"].ShouldBe("https://localhost:5111");
        File.Exists(DevAppInstancePaths.GetAssignmentFile(worktree)).ShouldBeTrue();
        File.Exists(DevAppInstancePaths.GetRegistryFile(this.repository.CommonDirectory)).ShouldBeTrue();
        File.Exists(DevAppInstancePaths.GetInventoryFile(sut.StateRoot)).ShouldBeTrue();
    }

    [Fact]
    public void Resolve_SecondStart_ReusesExistingAssignment()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var first = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Act
        var sut = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Assert
        sut.Slot.ShouldBe(first.Slot);
        sut.WorktreeId.ShouldBe(first.WorktreeId);
        sut.SelectionReason.ShouldBe(DevAppInstanceSelectionReason.Existing);
    }

    [Fact]
    public void Resolve_DeletedLocalFile_RecoversFromRegistry()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var first = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));
        File.Delete(DevAppInstancePaths.GetAssignmentFile(worktree));

        // Act
        var sut = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Assert
        sut.Slot.ShouldBe(first.Slot);
        sut.SelectionReason.ShouldBe(DevAppInstanceSelectionReason.Recovered);
    }

    [Fact]
    public void Resolve_TwoWorktrees_GetDistinctSlots()
    {
        // Arrange
        var wt1 = this.repository.AddWorktree("wt1");
        var wt2 = this.repository.AddWorktree("wt2");

        // Act
        var first = DevAppInstanceResolver.Resolve(this.repository.Request(wt1));
        var second = DevAppInstanceResolver.Resolve(this.repository.Request(wt2));

        // Assert
        first.Slot.ShouldBe(1);
        second.Slot.ShouldBe(2);
    }

    [Fact]
    public void Resolve_BlockedAndReservedPorts_SkipsSlots()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var request = this.repository.Request(worktree, probe: new FakePortProbe(5111), reservedPorts: [5210]);

        // Act
        var sut = DevAppInstanceResolver.Resolve(request);

        // Assert
        sut.Slot.ShouldBe(3);
    }

    [Fact]
    public void Resolve_CopiedAssignmentFile_ThrowsAssignmentInvalid()
    {
        // Arrange
        var wt1 = this.repository.AddWorktree("wt1");
        var wt2 = this.repository.AddWorktree("wt2");
        DevAppInstanceResolver.Resolve(this.repository.Request(wt1));
        Directory.CreateDirectory(Path.Combine(wt2, ".dev"));
        File.Copy(DevAppInstancePaths.GetAssignmentFile(wt1), DevAppInstancePaths.GetAssignmentFile(wt2));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.Resolve(this.repository.Request(wt2)))
            .Code.ShouldBe(DevAppInstanceErrorCodes.AssignmentInvalid);
    }

    [Fact]
    public void Resolve_ConflictingApplicationOffsets_ThrowsConfigurationConflict()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        DevAppInstanceResolver.Resolve(this.repository.Request(worktree));
        var other = new DevAppInstanceApplication("taskharbor", httpOffset: 11, httpsOffset: 12);

        // Act & Assert
        var exception = Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.Resolve(this.repository.Request(worktree, other)));
        exception.Code.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
        exception.Message.ShouldContain("forecasthub");
    }

    [Fact]
    public void Resolve_SecondApplication_SharesSlot()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var first = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));
        var other = new DevAppInstanceApplication("taskharbor");

        // Act
        var sut = DevAppInstanceResolver.Resolve(this.repository.Request(worktree, other));

        // Assert
        sut.Slot.ShouldBe(first.Slot);
        sut.HttpsPort.ShouldBe(5101);
        sut.AppId.ShouldBe("taskharbor");
    }

    [Fact]
    public void Resolve_AutomaticOffsets_AssignsLowestFreePairAndReusesIt()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        DevAppInstanceResolver.Resolve(this.repository.Request(worktree));
        var taskHarbor = DevAppInstanceApplication.FromApplicationName("Contoso.TaskHarbor.Presentation.Web.Server");
        var mailHub = DevAppInstanceApplication.FromApplicationName("Contoso.MailHub.Api");

        // Act
        var first = DevAppInstanceResolver.Resolve(this.repository.Request(worktree, taskHarbor));
        var second = DevAppInstanceResolver.Resolve(this.repository.Request(worktree, mailHub));
        var again = DevAppInstanceResolver.Resolve(this.repository.Request(worktree, taskHarbor));

        // Assert
        first.AppId.ShouldBe("taskharbor");
        first.HttpPort.ShouldBe(5100);
        first.HttpsPort.ShouldBe(5101);
        second.AppId.ShouldBe("mailhub");
        second.HttpsPort.ShouldBe(5103);
        again.HttpsPort.ShouldBe(5101);
        var recorded = DevAppInstanceRegistry.Load(DevAppInstancePaths.GetRegistryFile(this.repository.CommonDirectory)).Applications;
        recorded.Single(a => a.AppId == "taskharbor").Source.ShouldBe("Contoso.TaskHarbor.Presentation.Web.Server");
    }

    [Fact]
    public void Resolve_TwoHostsDeriveSameId_ThrowsConfigurationConflict()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        DevAppInstanceResolver.Resolve(this.repository.Request(worktree, DevAppInstanceApplication.FromApplicationName("Contoso.TaskHarbor.Web")));
        var other = DevAppInstanceApplication.FromApplicationName("Fabrikam.TaskHarbor.Api");

        // Act & Assert
        var exception = Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.Resolve(this.repository.Request(worktree, other)));
        exception.Code.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
        exception.Message.ShouldContain("Contoso.TaskHarbor.Web");
    }

    [Fact]
    public void Resolve_RequestedSlotOwnedByOtherWorktree_ThrowsSlotInUse()
    {
        // Arrange
        var wt1 = this.repository.AddWorktree("wt1");
        var wt2 = this.repository.AddWorktree("wt2");
        DevAppInstanceResolver.Resolve(this.repository.Request(wt1));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.Resolve(
                this.repository.Request(wt2, settings: new Dictionary<string, string> { [DevAppInstanceResolver.SlotKey] = "1" })))
            .Code.ShouldBe(DevAppInstanceErrorCodes.SlotInUse);
    }

    [Fact]
    public void Resolve_SlotOutsideLinkedWorktree_ReturnsInactiveWithWarning()
    {
        // Act
        var result = DevAppInstanceResolver.Resolve(
            this.repository.Request(this.repository.MainRoot, settings: new Dictionary<string, string> { [DevAppInstanceResolver.SlotKey] = "3" }));

        // Assert
        result.IsActive.ShouldBeFalse();
        result.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OrdinaryCheckout);
        result.LogSteps.ShouldContain(s => s.Template.StartsWith("[warning] ") && s.Template.Contains("{SlotKey}"));
    }

    [Fact]
    public void Resolve_InvalidModeInOrdinaryCheckout_ReturnsInactiveWithWarning()
    {
        // Act
        var result = DevAppInstanceResolver.Resolve(
            this.repository.Request(this.repository.MainRoot, settings: new Dictionary<string, string> { [DevAppInstanceResolver.ModeKey] = "Sometimes" }));

        // Assert
        result.IsActive.ShouldBeFalse();
        result.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OrdinaryCheckout);
        result.LogSteps.ShouldContain(s => s.Template.StartsWith("[warning] ") && s.Template.Contains("{ModeKey}"));
    }

    [Fact]
    public void Resolve_InvalidModeOutsideDevelopment_ReturnsInactive()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var request = this.repository.Request(
            worktree,
            settings: new Dictionary<string, string> { [DevAppInstanceResolver.ModeKey] = "Sometimes" },
            environmentName: "Production");

        // Act
        var result = DevAppInstanceResolver.Resolve(request);

        // Assert
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_InvalidModeInLinkedWorktree_ThrowsConfigurationConflict()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.Resolve(
                this.repository.Request(worktree, settings: new Dictionary<string, string> { [DevAppInstanceResolver.ModeKey] = "Sometimes" })))
            .Code.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
    }

    [Fact]
    public void ToStartupLogLine_WithoutOptOutInRecovery_AppendsHintOnce()
    {
        // Arrange
        var sut = new DevAppInstanceException(DevAppInstanceErrorCodes.PortInUse, "Port 5111 is in use.", "Stop the other process.");

        // Act
        var result = sut.ToStartupLogLine();

        // Assert
        result.ShouldStartWith(sut.ToLogLine());
        result.ShouldContain("DevAppInstance__Mode=Disabled");
    }

    [Fact]
    public void ToStartupLogLine_WithOptOutInRecovery_KeepsLogLine()
    {
        // Arrange
        var sut = new DevAppInstanceException(DevAppInstanceErrorCodes.ConfigurationConflict, "Conflict.", "Correct it or set DevAppInstance:Mode=Disabled.");

        // Act
        var result = sut.ToStartupLogLine();

        // Assert
        result.ShouldBe(sut.ToLogLine());
    }

    [Fact]
    public void GetStartupRecoveryAction_WithoutOptOutInRecovery_AppendsHint()
    {
        // Arrange
        var sut = new DevAppInstanceException(DevAppInstanceErrorCodes.PortInUse, "Port 5111 is in use.", "Stop the other process.");

        // Act
        var result = sut.GetStartupRecoveryAction();

        // Assert
        result.ShouldStartWith("Stop the other process.");
        result.ShouldContain("DevAppInstance__Mode=Disabled");
    }

    [Fact]
    public void GetStartupRecoveryAction_WithOptOutInRecovery_KeepsRecoveryAction()
    {
        // Arrange
        var sut = new DevAppInstanceException(DevAppInstanceErrorCodes.ConfigurationConflict, "Conflict.", "Correct it or set DevAppInstance:Mode=Disabled.");

        // Act
        var result = sut.GetStartupRecoveryAction();

        // Assert
        result.ShouldBe("Correct it or set DevAppInstance:Mode=Disabled.");
    }

    [Fact]
    public void FromUnexpected_PlainException_WrapsWithUnexpectedFailure()
    {
        // Arrange
        var inner = new UnauthorizedAccessException("Access denied.");

        // Act
        var result = DevAppInstanceException.FromUnexpected(inner, "forecasthub", 2);

        // Assert
        result.Code.ShouldBe(DevAppInstanceErrorCodes.UnexpectedFailure);
        result.InnerException.ShouldBeSameAs(inner);
        result.Message.ShouldContain("Access denied.");
        result.AppId.ShouldBe("forecasthub");
        result.Slot.ShouldBe(2);
    }

    [Fact]
    public void FromUnexpected_DevAppInstanceException_ReturnsSameInstance()
    {
        // Arrange
        var inner = new DevAppInstanceException(DevAppInstanceErrorCodes.PortInUse, "Port in use.", "Stop it.");

        // Act
        var result = DevAppInstanceException.FromUnexpected(inner);

        // Assert
        result.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Lease_SecondAcquire_ThrowsInstanceRunning()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var context = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));
        using var lease = DevAppInstanceLease.Acquire(context, TimeSpan.FromMilliseconds(200));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceLease.Acquire(context, TimeSpan.FromMilliseconds(200)))
            .Code.ShouldBe(DevAppInstanceErrorCodes.InstanceRunning);
    }

    [Fact]
    public void VerifyPortsAvailable_BlockedPort_ThrowsPortInUse()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var context = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.VerifyPortsAvailable(context, new FakePortProbe(5111)))
            .Code.ShouldBe(DevAppInstanceErrorCodes.PortInUse);
    }

    [Fact]
    public void ResolveOrphan_RecordedWorktree_ReturnsInactiveContextOfOrphanSlot()
    {
        // Arrange
        var wt1 = this.repository.AddWorktree("wt1");
        var wt2 = this.repository.AddWorktree("wt2");
        DevAppInstanceResolver.Resolve(this.repository.Request(wt1));
        var orphan = DevAppInstanceResolver.Resolve(this.repository.Request(wt2));

        // Act
        var sut = DevAppInstanceResolver.ResolveOrphan(this.repository.Request(this.repository.MainRoot), orphan.WorktreeId.Value);

        // Assert
        sut.Context.IsActive.ShouldBeFalse();
        sut.Context.Slot.ShouldBe(2);
        sut.Context.WorktreeId.ShouldBe(orphan.WorktreeId);
        sut.Context.GetInstanceDatabaseName("forecasthub_core").ShouldBe("forecasthub_core_w02");
        sut.Inventory.AppId.ShouldBe("forecasthub");
        sut.Inventory.Slot.ShouldBe(2);
    }

    [Fact]
    public void ResolveOrphan_UnknownWorktree_ThrowsResourceNotOwned()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.ResolveOrphan(this.repository.Request(this.repository.MainRoot), Guid.NewGuid()))
            .Code.ShouldBe(DevAppInstanceErrorCodes.ResourceNotOwned);
    }

    [Fact]
    public void ResolveOrphan_OtherApplication_ThrowsResourceNotOwned()
    {
        // Arrange
        var worktree = this.repository.AddWorktree("wt1");
        var context = DevAppInstanceResolver.Resolve(this.repository.Request(worktree));

        // Act & Assert
        Should.Throw<DevAppInstanceException>(() => DevAppInstanceResolver.ResolveOrphan(
                this.repository.Request(this.repository.MainRoot, new DevAppInstanceApplication("taskharbor")), context.WorktreeId.Value))
            .Code.ShouldBe(DevAppInstanceErrorCodes.ResourceNotOwned);
    }

    [Theory]
    [InlineData(new[] { "--bdk-dev-reset=forecasthub:w03" }, "forecasthub:w03")]
    [InlineData(new[] { "--other", "--bdk-dev-reset", "forecasthub:w03" }, "forecasthub:w03")]
    [InlineData(new[] { "--bdk-dev-reset" }, "")]
    [InlineData(new[] { "--urls=x" }, null)]
    public void Find_ReservedArgument_ReturnsValue(string[] args, string expected)
    {
        // Act
        var result = DevAppInstanceArguments.Find(args, DevAppInstanceArguments.Reset);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void FormatReset_InstanceToken_UsesTwoDigitSlot()
    {
        // Act
        var result = DevAppInstanceArguments.FormatReset("forecasthub", 3);

        // Assert
        result.ShouldBe("--bdk-dev-reset=forecasthub:w03");
    }
}
