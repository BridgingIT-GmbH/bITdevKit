// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.DevAppInstances;

using System.Text.Json;
using BridgingIT.DevKit.Common.UnitTests.Utilities.DevAppInstances;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Host-level tests of <see cref="DevKitWebApplicationBuilderDevAppInstanceExtensions"/>. Each builder gets a per-builder
/// resolve request seam. The class replaces the static exit seam, so its tests run sequentially and restore it on dispose.
/// </summary>
[UnitTest("Presentation")]
public sealed class DevAppInstanceHostTests : IDisposable
{
    private readonly FakeGitRepository repository = new();
    private readonly List<IDisposable> disposables = [];
    private readonly Func<DevAppInstanceResolveRequest, DevAppInstanceResolveRequest> customizer;
    private string contentRoot;

    public DevAppInstanceHostTests()
    {
        this.customizer = request => new DevAppInstanceResolveRequest
        {
            Application = request.Application,
            PortPolicy = request.PortPolicy,
            ReservedPorts = request.ReservedPorts,
            ContentRoot = this.contentRoot,
            EnvironmentName = "Development",
            Configuration = request.Configuration,
            Resources = request.Resources,
            DatabaseAdapters = request.DatabaseAdapters,
            SharedKeys = request.SharedKeys,
            AuthorityKeys = request.AuthorityKeys,
            GitRunner = null,
            PortProbe = new FakePortProbe(),
            GetEnvironmentVariable = _ => null,
            IsBuildTime = false,
        };
        DevAppInstanceResetRunner.Exit = code => throw new ExitException(code);
    }

    public void Dispose()
    {
        DevAppInstanceResetRunner.Exit = Environment.Exit;
        foreach (var disposable in this.disposables)
        {
            disposable.Dispose();
        }

        this.repository.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddDevAppInstance_DevelopmentHost_LocatesDatabaseAdaptersOnlyInLinkedWorktree(bool linked)
    {
        // Arrange
        this.contentRoot = linked ? this.repository.AddWorktree("feature-a") : this.repository.MainRoot;
        IReadOnlyList<IDevAppInstanceDatabaseAdapter> captured = null;
        var sut = DevKitWebApplication.CreateBuilder(["--environment", "Development"]);
        sut.Properties[DevKitWebApplicationBuilderDevAppInstanceExtensions.RequestCustomizerPropertyKey] =
            (Func<DevAppInstanceResolveRequest, DevAppInstanceResolveRequest>)(request =>
            {
                captured = request.DatabaseAdapters;

                return this.customizer(request);
            });

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        if (linked)
        {
            this.TrackState(sut);
        }

        // Assert
        sut.GetDevAppInstance().IsActive.ShouldBe(linked);
        var deferred = captured.ShouldBeOfType<DevKitWebApplicationBuilderDevAppInstanceExtensions.DeferredList<IDevAppInstanceDatabaseAdapter>>();
        deferred.IsMaterialized.ShouldBe(linked);
    }

    [Fact]
    public void AddDevAppInstance_OrdinaryCheckout_LeavesInactiveFootprint()
    {
        // Arrange
        this.contentRoot = this.repository.MainRoot;
        var sut = this.CreateBuilder([]);

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeFalse();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.OrdinaryCheckout);
        sut.Services.ShouldNotContain(d => d.ServiceType == typeof(DevAppInstanceRuntimeState));
        sut.Services.ShouldNotContain(d => d.ImplementationType == typeof(DevAppInstanceLifecycleService));
        sut.Configuration["Serilog:Properties:DevAppInstanceAppId"].ShouldBeNull();
        Directory.Exists(Path.Combine(this.repository.CommonDirectory, "bdk-dev")).ShouldBeFalse();
    }

    [Fact]
    public void AddDevAppInstance_OrdinaryCheckoutWithInvalidSettings_StartsInactiveWithWarnings()
    {
        // Arrange
        this.contentRoot = this.repository.MainRoot;
        var sut = this.CreateBuilder([]);
        sut.Configuration[DevAppInstanceResolver.ModeKey] = "Sometimes";
        sut.Configuration[DevAppInstanceResolver.SlotKey] = "abc";

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeFalse();
        context.LogSteps.Count(s => s.Template.StartsWith("[warning] ")).ShouldBe(2);
        sut.Services.ShouldContain(d => d.ImplementationType == typeof(DevAppInstanceStatusLogService));
    }

    [Fact]
    public void GetListenUrls_WildcardAndMixedSchemes_ReturnsLocalhostUrlsHttpsFirst()
    {
        // Arrange
        string[] addresses = ["http://localhost:5000", "https://*:5001", "https://[::]:5001/"];

        // Act
        var result = DevAppInstanceStatusLogService.GetListenUrls(addresses);

        // Assert
        result.ShouldBe(["https://localhost:5001", "http://localhost:5000"]);
    }

    [Fact]
    public void AddDevAppInstance_UnexpectedFailure_FallsBackToDefaults()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = DevKitWebApplication.CreateBuilder([]);
        Func<DevAppInstanceResolveRequest, DevAppInstanceResolveRequest> failing = _ => throw new IOException("Disk not ready.");
        sut.Properties[DevKitWebApplicationBuilderDevAppInstanceExtensions.RequestCustomizerPropertyKey] = failing;

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub"));

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeFalse();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Fallback);
        context.FailureCode.ShouldBe(DevAppInstanceErrorCodes.UnexpectedFailure);
        context.FailureMessage.ShouldContain("Disk not ready.");
    }

    [Fact]
    public void AddDevAppInstance_LinkedWorktree_RegistersManagedServicesAndLease()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        var state = this.TrackState(sut);

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeTrue();
        context.Slot.ShouldBe(1);
        state.Lease.ShouldNotBeNull();
        sut.Services.ShouldContain(d => d.ImplementationType == typeof(DevAppInstanceLifecycleService));
        sut.Configuration["urls"].ShouldBe($"{context.HttpsUrl};{context.HttpUrl}");
        sut.Configuration["Serilog:Properties:DevAppInstanceAppId"].ShouldBe("forecasthub");
    }

    [Fact]
    public void AddDevAppInstance_NoDeclaration_DerivesIdAndAssignsOffsets()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);

        // Act
        sut.AddDevAppInstance();
        this.TrackState(sut);

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeTrue();
        context.AppId.ShouldBe(DevAppInstanceApplication.DeriveAppId(sut.Environment.ApplicationName));
        context.HttpPort.ShouldBe(5100);
        context.HttpsPort.ShouldBe(5101);
    }

    [Fact]
    public async Task Build_LinkedWorktree_SuffixesAntiforgeryCookieName()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddAntiforgery();

        // Act
        await using var app = sut.Build();

        // Assert
        var options = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        options.Cookie.Name.ShouldStartWith(".AspNetCore.Antiforgery");
        options.Cookie.Name.ShouldEndWith(".forecasthub.w01");
    }

    [Fact]
    public void AddDevAppInstance_ResetOnOrdinaryCheckout_RefusesWithExitCode()
    {
        // Arrange
        this.contentRoot = this.repository.MainRoot;
        var sut = this.CreateBuilder(["--bdk-dev-reset=forecasthub:w01"]);

        // Act
        var exit = Should.Throw<ExitException>(() =>
            sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11)));

        // Assert
        exit.Code.ShouldBe(DevAppInstanceArguments.ExitCodes.Refused);
    }

    [Fact]
    public void AddDevAppInstance_ResetTokenOfOtherInstance_RefusesWithoutLease()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder(["--bdk-dev-reset=forecasthub:w02"]);

        // Act
        var exit = Should.Throw<ExitException>(() =>
            sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11)));

        // Assert
        exit.Code.ShouldBe(DevAppInstanceArguments.ExitCodes.Refused);
        sut.Services.ShouldNotContain(d => d.ServiceType == typeof(DevAppInstanceRuntimeState));
    }

    [Fact]
    public async Task Build_LaterSourceOverridesManagedValue_FallsBackToDefaults()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["https_port"] = "1234" });

        // Act
        await using var app = sut.Build();

        // Assert
        var context = sut.GetDevAppInstance();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Fallback);
        context.FailureCode.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
        context.FailureMessage.ShouldContain("https_port");
        app.Configuration["https_port"].ShouldBe("1234");
        app.Services.GetService<DevAppInstanceRuntimeState>().ShouldBeNull();
    }

    [Fact]
    public async Task Build_LaterSourceWithoutManagedKeys_KeepsManagedSourceLast()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["Feature:Enabled"] = "true" });

        // Act
        await using var app = sut.Build();

        // Assert
        ((IConfigurationBuilder)sut.Configuration).Sources[^1].ShouldBeOfType<DevAppInstanceConfigurationSource>();
        app.Configuration["Feature:Enabled"].ShouldBe("true");
    }

    [Fact]
    public async Task Build_FakeIdentityProviderWithDefaultIssuer_AlignsIssuerAndCookies()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddFakeIdentityProvider(o => o
            .Enabled(true)
            .WithClient("ForecastHub", "forecasthub-web", "/authentication/login-callback"));
        var context = sut.GetDevAppInstance();

        // Act
        await using var app = sut.Build();

        // Assert
        app.Services.GetRequiredService<FakeIdentityProviderEndpointsOptions>().Issuer.ShouldBe(context.PublicOrigin);
        var cookies = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        cookies.Get(FakeIdentityProviderAuthenticationDefaults.CookieScheme).Cookie.Name.ShouldBe(context.IdentityProviderCookieName);
    }

    [Fact]
    public async Task Build_FakeIdentityProviderWithOtherLoopbackIssuer_FallsBackToDefaults()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddFakeIdentityProvider(o => o.Enabled(true).WithIssuer("https://localhost:7001"));

        // Act
        await using var app = sut.Build();

        // Assert
        var context = sut.GetDevAppInstance();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Fallback);
        context.FailureCode.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
        context.FailureMessage.ShouldContain("issuer");
    }

    [Fact]
    public async Task Build_FakeIdentityProviderWithLoopbackCallbackOnOtherOrigin_FallsBackToDefaults()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddFakeIdentityProvider(o => o
            .Enabled(true)
            .WithClient("ForecastHub", "forecasthub-web", "https://localhost:5001/authentication/login-callback"));

        // Act
        await using var app = sut.Build();

        // Assert
        var context = sut.GetDevAppInstance();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Fallback);
        context.FailureCode.ShouldBe(DevAppInstanceErrorCodes.ConfigurationConflict);
        context.FailureMessage.ShouldContain("forecasthub-web");
    }

    [Fact]
    public async Task Build_FakeIdentityProviderWithExternalIssuer_KeepsIssuerAndCallbacks()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddFakeIdentityProvider(o => o
            .Enabled(true)
            .WithIssuer("https://identity.example")
            .WithClient("ForecastHub", "forecasthub-web", "https://localhost:5001/authentication/login-callback"));

        // Act
        await using var app = sut.Build();

        // Assert
        app.Services.GetRequiredService<FakeIdentityProviderEndpointsOptions>().Issuer.ShouldBe("https://identity.example");
    }

    [Fact]
    public async Task Build_LinkedWorktree_SuffixesDashboardCookieName()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddAuthentication().AddCookie(DashboardAuthenticationDefaults.CookieScheme, o => o.Cookie.Name = ".Bdk.Dashboard");
        var context = sut.GetDevAppInstance();

        // Act
        await using var app = sut.Build();

        // Assert
        var cookies = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        cookies.Get(DashboardAuthenticationDefaults.CookieScheme).Cookie.Name.ShouldBe(context.DashboardCookieName);
    }

    [Fact]
    public async Task Build_LinkedWorktree_SuffixesAppAuthenticationSessionAndTempDataCookies()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        this.TrackState(sut);
        sut.Services.AddAuthentication()
            .AddCookie("Identity", o => o.Cookie.Name = ".AspNetCore.Identity")
            .AddCookie("Partner");
        sut.Services.AddSession(o => o.Cookie.Name = ".ForecastHub.Session");
        sut.Services.AddControllersWithViews().AddCookieTempDataProvider();

        // Act
        await using var app = sut.Build();

        // Assert
        var cookies = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        cookies.Get("Identity").Cookie.Name.ShouldBe(".AspNetCore.Identity.forecasthub.w01");
        cookies.Get("Partner").Cookie.Name.ShouldBe($"{CookieAuthenticationDefaults.CookiePrefix}Partner.forecasthub.w01");
        app.Services.GetRequiredService<IOptions<SessionOptions>>().Value.Cookie.Name.ShouldBe(".ForecastHub.Session.forecasthub.w01");
        app.Services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name
            .ShouldBe($"{CookieTempDataProvider.CookieName}.forecasthub.w01");
    }

    [Fact]
    public async Task Build_OrdinaryCheckout_KeepsCookieNames()
    {
        // Arrange
        this.contentRoot = this.repository.MainRoot;
        var sut = this.CreateBuilder([]);
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").WithHttpOffset(http: 10, https: 11));
        sut.Services.AddAuthentication().AddCookie("Identity", o => o.Cookie.Name = ".AspNetCore.Identity");
        sut.Services.AddSession();

        // Act
        await using var app = sut.Build();

        // Assert
        app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Identity").Cookie.Name.ShouldBe(".AspNetCore.Identity");
        app.Services.GetRequiredService<IOptions<SessionOptions>>().Value.Cookie.Name.ShouldBe(SessionDefaults.CookieName);
    }

    [Fact]
    public void AddFakeIdentityProvider_WithCookie_AppliesCookieHookAfterDefaults()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        services.AddFakeIdentityProvider(o => o.Enabled(true).WithCookie(c => c.Cookie.Name = ".ForecastHub.Identity"));

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        var cookies = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        cookies.Get(FakeIdentityProviderAuthenticationDefaults.CookieScheme).Cookie.Name.ShouldBe(".ForecastHub.Identity");
    }

    [Fact]
    public void GetDevAppInstance_WithoutAddDevAppInstance_ActivatesDefaultsInLinkedWorktree()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);

        // Act
        var context = sut.GetDevAppInstance();
        this.TrackState(sut);

        // Assert
        context.IsActive.ShouldBeTrue();
        context.AppId.ShouldBe(DevAppInstanceApplication.DeriveAppId(sut.Environment.ApplicationName));
        sut.Configuration["urls"].ShouldBe($"{context.HttpsUrl};{context.HttpUrl}");
    }

    [Fact]
    public async Task Build_WithoutAddDevAppInstance_ActivatesDefaultsAndValidates()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        sut.Services.AddAntiforgery();

        // Act
        await using var app = sut.Build();
        this.disposables.Add(app.Services.GetRequiredService<DevAppInstanceRuntimeState>());

        // Assert
        var context = app.Services.GetRequiredService<DevAppInstanceContext>();
        context.IsActive.ShouldBeTrue();
        app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name.ShouldEndWith($".{context.AppId}.w01");
    }

    [Fact]
    public void ResolveConnectionString_BeforeActivation_RunsDefaultActivation()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);
        var adapter = Substitute.For<IDevAppInstanceDatabaseAdapter>();
        adapter.Kind.Returns("fake");

        // Act
        var result = DevAppInstanceSession.ResolveConnectionString(sut.Services, adapter, null, "ForecastDbContext");

        // Assert
        result.ShouldBeNull();
        sut.Properties.ShouldContainKey(DevKitWebApplicationBuilderDevAppInstanceExtensions.ContextPropertyKey);
        sut.Properties.ShouldNotContainKey(DevKitBuilderProperties.ConfigurationCompletion);
        this.TrackState(sut);
    }

    [Fact]
    public void AddDevAppInstance_ModeDisabledInLinkedWorktree_KeepsConfiguredPortsAndDatabases()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = DevKitWebApplication.CreateBuilder([]);
        Func<DevAppInstanceResolveRequest, DevAppInstanceResolveRequest> failing = _ => throw new IOException("Disk not ready.");
        sut.Properties[DevKitWebApplicationBuilderDevAppInstanceExtensions.RequestCustomizerPropertyKey] = failing;
        sut.Configuration[DevAppInstanceResolver.ModeKey] = "disabled";
        sut.Configuration["urls"] = "https://localhost:5011;http://localhost:5010";
        const string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=ForecastHub;Trusted_Connection=True";
        var adapter = Substitute.For<IDevAppInstanceDatabaseAdapter>();
        adapter.Kind.Returns("fake");

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub"));
        sut.CompleteConfiguration();
        var resolved = DevAppInstanceSession.ResolveConnectionString(sut.Services, adapter, connectionString, "ForecastDbContext");

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeFalse();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Disabled);
        sut.Configuration["urls"].ShouldBe("https://localhost:5011;http://localhost:5010");
        resolved.ShouldBe(connectionString);
        sut.Services.ShouldNotContain(d => d.ServiceType == typeof(DevAppInstanceRuntimeState));
        Directory.Exists(Path.Combine(this.repository.CommonDirectory, "bdk-dev")).ShouldBeFalse();
    }

    [Fact]
    public void AddDevAppInstance_EnabledFalse_RegistersDisabledContext()
    {
        // Arrange
        this.contentRoot = this.repository.AddWorktree("feature-a");
        var sut = this.CreateBuilder([]);

        // Act
        sut.AddDevAppInstance(o => o.WithAppId("forecasthub").Enabled(false));
        sut.CompleteConfiguration();

        // Assert
        var context = sut.GetDevAppInstance();
        context.IsActive.ShouldBeFalse();
        context.InactiveReason.ShouldBe(DevAppInstanceInactiveReasons.Disabled);
        context.AppId.ShouldBe("forecasthub");
        sut.Services.ShouldNotContain(d => d.ServiceType == typeof(DevAppInstanceRuntimeState));
        sut.Configuration["urls"].ShouldBeNull();
        Directory.Exists(Path.Combine(this.repository.CommonDirectory, "bdk-dev")).ShouldBeFalse();
    }

    [Fact]
    public void AddDevAppInstance_AfterDefaultActivation_Throws()
    {
        // Arrange
        this.contentRoot = this.repository.MainRoot;
        var sut = this.CreateBuilder([]);
        sut.CompleteConfiguration();

        // Act
        var act = () => sut.AddDevAppInstance(o => o.WithAppId("forecasthub"));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("before AddLogging");
    }

    private DevKitWebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = DevKitWebApplication.CreateBuilder(args);
        builder.Properties[DevKitWebApplicationBuilderDevAppInstanceExtensions.RequestCustomizerPropertyKey] = this.customizer;

        return builder;
    }

    private DevAppInstanceRuntimeState TrackState(DevKitWebApplicationBuilder builder)
    {
        var state = (DevAppInstanceRuntimeState)builder.Services
            .Single(d => d.ServiceType == typeof(DevAppInstanceRuntimeState))
            .ImplementationInstance;
        this.disposables.Add(state);

        return state;
    }

    [Fact]
    public void WriteStep_WarningStep_PassesLogKeyAsStructuredArgument()
    {
        // Arrange
        var logger = new RecordingLogger();
        var step = new DevAppInstanceLogStep("[warning] slot {Slot} assigned (app={AppId})", [2, "forecasthub"]);

        // Act
        DevAppInstanceLifecycleService.WriteStep(logger, step);

        // Assert
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldBe("[BDK] worktree slot 2 assigned (app=forecasthub)");
        entry.Properties["{OriginalFormat}"].ShouldBe("[{LogKey}] worktree slot {Slot} assigned (app={AppId})");
        entry.Properties["LogKey"].ShouldBe("BDK");
    }

    [Fact]
    public void LogFailure_StartupError_PassesLogKeyAndErrorCodeAsStructuredArguments()
    {
        // Arrange
        var logger = new RecordingLogger();
        var exception = new DevAppInstanceException(DevAppInstanceErrorCodes.PortInUse, "Port 5111 is in use.", "Stop the other process.")
        { AppId = "forecasthub", Slot = 2 };

        // Act
        DevAppInstanceLifecycleService.LogFailure(logger, exception, exception.GetStartupRecoveryAction());

        // Assert
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldStartWith("[BDK] worktree failure: Port 5111 is in use. (code=port_in_use, app=forecasthub, slot=2)");
        entry.Properties["LogKey"].ShouldBe("BDK");
        entry.Properties["ErrorCode"].ShouldBe(DevAppInstanceErrorCodes.PortInUse);
        entry.Properties["RecoveryAction"].ToString().ShouldContain("DevAppInstance__Mode=Disabled");
    }

    private sealed record RecordedLogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object> Properties);

    private sealed class RecordingLogger : ILogger
    {
        public List<RecordedLogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : [];
            this.Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed class ExitException(int code) : Exception($"exit {code}")
    {
        public int Code { get; } = code;
    }
}
