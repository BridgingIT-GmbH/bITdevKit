// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.DevAppInstances;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

public class DevAppInstanceSessionTests : IDisposable
{
    private const string CoreKey = "Modules:Core:ConnectionStrings:Default";
    private const string ReportingKey = "ConnectionStrings:Reporting";
    private const string Baseline = "Server=127.0.0.1,14333;Database=forecasthub_core;User Id=sa;Password=secret;TrustServerCertificate=True";
    private const string Reporting = "Server=127.0.0.1,14333;Database=reporting;User Id=sa;Password=secret;TrustServerCertificate=True";

    private readonly FakeGitRepository repository = new();
    private readonly List<IReadOnlyDictionary<string, string>> applied = [];

    public void Dispose() => this.repository.Dispose();

    [Fact]
    public void ResolveConnectionString_NoSession_ReturnsUnchanged()
    {
        // Act
        var result = DevAppInstanceSession.ResolveConnectionString(new ServiceCollection(), SqlServerDevAppInstanceAdapter.Instance, Baseline, "CoreDbContext");

        // Assert
        result.ShouldBe(Baseline);
    }

    [Fact]
    public void ResolveConnectionString_OrdinaryCheckout_ReturnsUnchangedAndRecordsAdapter()
    {
        // Arrange
        var sut = this.CreateSession(this.repository.MainRoot);

        // Act
        var result = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, Baseline, "CoreDbContext");

        // Assert
        result.ShouldBe(Baseline);
        sut.Adapters.ShouldContain(SqlServerDevAppInstanceAdapter.Instance);
        this.applied.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveConnectionString_NewAdapterInLinkedWorktree_PlansDatabaseAndAppliesManagedValues()
    {
        // Arrange
        var services = new ServiceCollection();
        var sut = this.CreateSession(this.repository.AddWorktree("wt1"));
        services.AddSingleton(sut);

        // Act
        var result = DevAppInstanceSession.ResolveConnectionString(services, SqlServerDevAppInstanceAdapter.Instance, Baseline, "CoreDbContext");

        // Assert
        new SqlConnectionStringBuilder(result).InitialCatalog.ShouldBe("forecasthub_core_w01");
        sut.Context.Resources.Select(r => r.Target).ShouldBe(["forecasthub_core_w01", "reporting_w01"], ignoreOrder: true);
        sut.DeclaredKeys.ShouldContain(CoreKey);
        sut.Baseline[CoreKey].ShouldBe(Baseline);
        new SqlConnectionStringBuilder(this.applied.Single()[CoreKey]).InitialCatalog.ShouldBe("forecasthub_core_w01");
    }

    [Fact]
    public void ResolveConnectionString_SecondRegistrationOfSameKind_DoesNotPlanAgain()
    {
        // Arrange
        var sut = this.CreateSession(this.repository.AddWorktree("wt1"));
        var first = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, Baseline, "CoreDbContext");

        // Act
        var second = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, Baseline, "AuditDbContext");
        var managed = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, first, "OutboxDbContext");

        // Assert
        second.ShouldBe(first);
        managed.ShouldBe(first);
        this.applied.Count.ShouldBe(1);
        sut.Context.Resources.Count.ShouldBe(2);
    }

    [Fact]
    public void ResolveConnectionString_SharedConnectionString_ReturnsUnchanged()
    {
        // Arrange
        var sut = this.CreateSession(this.repository.AddWorktree("wt1"), sharedKeys: [ReportingKey]);

        // Act
        var result = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, Reporting, "ReportingDbContext");

        // Assert
        result.ShouldBe(Reporting);
        sut.Context.Resources.Where(r => !r.Shared).Select(r => r.Target).ShouldBe(["forecasthub_core_w01"]);
    }

    [Fact]
    public void ResolveConnectionString_UndeclaredDatabase_FallsBackAndKeepsConnectionString()
    {
        // Arrange
        var sut = this.CreateSession(this.repository.AddWorktree("wt1"));
        var undeclared = Baseline.Replace("forecasthub_core", "elsewhere");

        // Act
        var result = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, undeclared, "ElsewhereDbContext");

        // Assert
        result.ShouldBe(undeclared);
        sut.Context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Fallback);
        sut.Context.FailureCode.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
    }

    [Fact]
    public void FallBack_CalledTwice_UndoesActivationOnce()
    {
        // Arrange
        var undone = 0;
        var sut = new DevAppInstanceSession(
            new DevAppInstanceResolveRequest(),
            this.CreateSession(this.repository.AddWorktree("wt1")).Context,
            [],
            null,
            null,
            _ => undone++);
        var failure = new DevAppInstanceException(DevAppInstanceErrorCodes.ConfigurationConflict, "Conflict.", "Fix it.");

        // Act
        sut.FallBack(failure);
        sut.FallBack(failure);

        // Assert
        undone.ShouldBe(1);
        sut.Context.IsActive.ShouldBeFalse();
        sut.Context.FailureMessage.ShouldBe("Conflict.");
    }

    [Fact]
    public void ResolveConnectionString_AdapterKnownAtResolve_UsesExistingPlan()
    {
        // Arrange
        var sut = this.CreateSession(this.repository.AddWorktree("wt1"), adapters: [SqlServerDevAppInstanceAdapter.Instance]);

        // Act
        var result = sut.ResolveConnectionString(SqlServerDevAppInstanceAdapter.Instance, Baseline, "CoreDbContext");

        // Assert
        new SqlConnectionStringBuilder(result).InitialCatalog.ShouldBe("forecasthub_core_w01");
        this.applied.ShouldBeEmpty();
    }

    private DevAppInstanceSession CreateSession(
        string contentRoot,
        IReadOnlyList<IDevAppInstanceDatabaseAdapter> adapters = null,
        IReadOnlyCollection<string> sharedKeys = null)
    {
        var settings = new Dictionary<string, string> { [CoreKey] = Baseline, [ReportingKey] = Reporting };
        var request = this.repository.Request(contentRoot, settings: settings, databaseAdapters: adapters ?? [], sharedKeys: sharedKeys ?? []);
        var context = DevAppInstanceResolver.Resolve(request);
        var baseline = (context.Resources ?? [])
            .SelectMany(r => r.SourceKeys ?? [])
            .ToDictionary(k => k, k => settings[k], StringComparer.OrdinalIgnoreCase);

        return new DevAppInstanceSession(request, context, adapters ?? [], baseline, this.applied.Add);
    }
}
