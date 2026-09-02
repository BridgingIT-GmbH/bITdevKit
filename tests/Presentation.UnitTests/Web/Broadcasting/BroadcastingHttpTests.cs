// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Broadcasting;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[Collection(BroadcastingHttpTestCollection.Name)]
public class BroadcastingHttpTests
{
    [Theory]
    [InlineData("ftp://node-a")]
    [InlineData("http://0.0.0.0:5000")]
    [InlineData("https://user:password@node-a")]
    public void AdvertisedAddress_NonDirectOrCredentialBearingAddress_ThrowsArgumentException(
        string address
    )
    {
        // Arrange
        var sut = new BroadcastingHttpOptionsBuilder(new BroadcastingHttpOptions());

        // Act
        var action = () => sut.AdvertisedAddress(address);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task HttpTransport_WithCorrelation_AddsMiddlewareCompatibleHeader()
    {
        // Arrange
        var handler = new CorrelationRecordingHandler();
        using var client = new HttpClient(handler);
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(HttpBroadcastTransport.ClientName).Returns(client);
        var sut = new HttpBroadcastTransport(
            clientFactory,
            new AllowAllBroadcastHttpAuthentication()
        );
        var now = DateTimeOffset.UtcNow;
        var envelope = new BroadcastEnvelope(
            Guid.NewGuid(),
            typeof(TestBroadcast).FullName,
            ["Alpha"],
            Serialize(new TestBroadcast("value")),
            now,
            now.AddMinutes(1),
            "correlation-123"
        );

        // Act
        var result = await sut.SendAsync(
            new BroadcastNodeRegistration
            {
                NodeIdentity = "node-b",
                AdvertisedAddress = new Uri("https://node-b/_bdk/api/broadcasting"),
            },
            envelope
        );

        // Assert
        result.Outcome.ShouldBe(BroadcastDeliveryOutcome.Accepted);
        handler.CapturedCorrelationId.ShouldBe("correlation-123");
        handler.HasLegacyCorrelationHeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RequestCorrelation_WithSuppliedId_ExposesCorrelationSeparateFromTraceId()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseRequestCorrelation();
        app.MapGet(
            "/correlation",
            () =>
                Results.Json(
                    new CorrelationResponse(
                        CorrelationId.Current,
                        Activity.Current?.TraceId.ToString()
                    )
                )
        );
        await app.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/correlation");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, "correlation-123");

        // Act
        using var response = await app.GetTestClient().SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<CorrelationResponse>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        payload.ShouldNotBeNull();
        payload.CorrelationId.ShouldBe("correlation-123");
        payload.CorrelationId.ShouldNotBe(payload.TraceId);
        CorrelationId.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("development secret")]
    public async Task SharedSecretAuthentication_MatchingExactValue_Authenticates(string secret)
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .AddBroadcasting(options => options.Enabled(false).Scopes("Alpha"))
            .WithHttpTransport(options => options.SharedSecret(secret));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BroadcastingHttpOptions>();
        var sut = provider.GetRequiredService<IBroadcastHttpAuthentication>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://node/broadcast");
        await sut.ApplyAsync(request);
        var context = new DefaultHttpContext();
        if (
            request.Headers.TryGetValues(
                SharedSecretBroadcastHttpAuthentication.HeaderName,
                out var values
            )
        )
        {
            context.Request.Headers[SharedSecretBroadcastHttpAuthentication.HeaderName] =
                values.ToArray();
        }

        // Act
        var result = await sut.AuthenticateAsync(context);

        // Assert
        result.ShouldBeTrue();
        options.SharedSecret.ShouldBe(secret ?? string.Empty);
    }

    [Fact]
    public async Task SharedSecretAuthentication_MultipleValues_RejectsRequest()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .AddBroadcasting(options => options.Enabled(false).Scopes("Alpha"))
            .WithHttpTransport(options => options.SharedSecret("secret"));
        using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IBroadcastHttpAuthentication>();
        var context = new DefaultHttpContext();
        context.Request.Headers[SharedSecretBroadcastHttpAuthentication.HeaderName] =
        [
            Convert.ToBase64String("secret"u8),
            Convert.ToBase64String("secret"u8),
        ];

        // Act
        var result = await sut.AuthenticateAsync(context);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task BroadcastEndpoint_FallbackBearerPolicy_UsesOnlyDedicatedSharedSecret()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder
            .Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationScheme,
                _ => { }
            );
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder
            .Services.AddBroadcasting(options => options.Scopes("Alpha"))
            .AddHandler<TestBroadcast, TestBroadcastHandler>()
            .WithHttpTransport(options => options.SharedSecret("dev"));

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", () => Results.Ok());
        app.MapEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();
        var envelope = new BroadcastEnvelope(
            Guid.NewGuid(),
            typeof(TestBroadcast).FullName,
            ["Alpha"],
            Serialize(new TestBroadcast("value")),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(1)
        );

        // Act
        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/_bdk/api/broadcasting")
        {
            Content = JsonContent.Create(envelope),
        };
        validRequest.Headers.TryAddWithoutValidation(
            SharedSecretBroadcastHttpAuthentication.HeaderName,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("dev"))
        );
        var validBroadcast = await client.SendAsync(validRequest);

        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, "/_bdk/api/broadcasting")
        {
            Content = JsonContent.Create(envelope with { BroadcastId = Guid.NewGuid() }),
        };
        wrongRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            TestAuthenticationHandler.AuthenticationScheme,
            "valid"
        );
        wrongRequest.Headers.TryAddWithoutValidation(
            SharedSecretBroadcastHttpAuthentication.HeaderName,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong"))
        );
        var wrongSecret = await client.SendAsync(wrongRequest);
        var protectedWithoutBearer = await client.GetAsync("/protected");

        // Assert
        validBroadcast.StatusCode.ShouldBe(HttpStatusCode.OK);
        wrongSecret.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        protectedWithoutBearer.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NodeAddressResolver_CustomResolver_PrecedesKestrelAndAppendsRoute()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .AddBroadcasting(options => options.Enabled(false).Scopes("Alpha"))
            .AddNodeAddressResolver<TestNodeAddressResolver>(10)
            .WithHttpTransport();
        using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IBroadcastNodeAddressResolver>();

        // Act
        var result = await sut.ResolveAsync();

        // Assert
        result.ShouldBe(new Uri("https://custom-node/_bdk/api/broadcasting"));
    }

    [Fact]
    public async Task PublishAsync_TwoKestrelNodes_DeliversLocallyAndThroughSharedSecretHttp()
    {
        // Arrange
        var registry = new SharedBroadcastRegistry();
        var probe = new BroadcastHandlerProbe();
        await using var nodeA = CreateNode("node-a", registry, probe);
        await using var nodeB = CreateNode("node-b", registry, probe);
        await nodeA.StartAsync();
        await nodeB.StartAsync();
        var sut = nodeA.Services.GetRequiredService<IBroadcastService>();

        // Act
        var result = await sut.PublishAsync(new TestBroadcast("two-nodes"), ["Alpha"]);
        var handledNodes = await probe.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetCount.ShouldBe(2);
        result.Value.AcceptedCount.ShouldBe(2);
        handledNodes.OrderBy(value => value, StringComparer.Ordinal).ShouldBe(["node-a", "node-b"]);
        await nodeB.StopAsync();
        await nodeA.StopAsync();
    }

    [Fact]
    public async Task BroadcastEndpoint_DisabledRuntime_DoesNotMapRoute()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder
            .Services.AddBroadcasting(options => options.Enabled(false))
            .WithHttpTransport(options => options.SharedSecret());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        // Act
        var response = await app.GetTestClient()
            .PostAsync("/_bdk/api/broadcasting", JsonContent.Create(new { }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static byte[] Serialize(TestBroadcast value)
    {
        using var stream = new MemoryStream();
        new SystemTextJsonSerializer().Serialize(value, stream);
        return stream.ToArray();
    }

    private static WebApplication CreateNode(
        string identity,
        IBroadcastRegistryStore registry,
        BroadcastHandlerProbe probe
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(probe);
        builder
            .Services.AddBroadcasting(options => options.Scopes("Alpha").NodeIdentity(identity))
            .AddHandler<TestBroadcast, ProbedBroadcastHandler>()
            .WithHttpTransport(options => options.SharedSecret("two-node-secret"));

        var app = builder.Build();
        app.MapEndpoints();
        return app;
    }

    public sealed record TestBroadcast(string Value);

    public sealed record CorrelationResponse(string CorrelationId, string TraceId);

    public sealed class TestBroadcastHandler : IBroadcastHandler<TestBroadcast>
    {
        public Task HandleAsync(
            TestBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    public sealed class TestNodeAddressResolver : IBroadcastNodeAddressResolver
    {
        public ValueTask<Uri> ResolveAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new Uri("https://custom-node"));
    }

    public sealed class ProbedBroadcastHandler(
        IBroadcastNodeIdentityProvider identityProvider,
        BroadcastHandlerProbe probe
    ) : IBroadcastHandler<TestBroadcast>
    {
        public Task HandleAsync(
            TestBroadcast payload,
            BroadcastContext context,
            CancellationToken cancellationToken
        )
        {
            probe.Record(identityProvider.GetNodeIdentity());
            return Task.CompletedTask;
        }
    }

    public sealed class BroadcastHandlerProbe
    {
        private readonly ConcurrentDictionary<string, byte> nodes = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource<IReadOnlyCollection<string>> completed = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public void Record(string nodeIdentity)
        {
            this.nodes.TryAdd(nodeIdentity, 0);
            if (this.nodes.Count == 2)
            {
                this.completed.TrySetResult(this.nodes.Keys.ToArray());
            }
        }

        public Task<IReadOnlyCollection<string>> WaitAsync(TimeSpan timeout) =>
            this.completed.Task.WaitAsync(timeout);
    }

    private sealed class SharedBroadcastRegistry : IBroadcastRegistryStore
    {
        private readonly InMemoryBroadcastRegistryStore inner = new(
            new BroadcastingOptions(),
            TimeProvider.System
        );

        public BroadcastRegistryCapabilities Capabilities { get; } = new(true, true);

        public Task UpsertAsync(
            BroadcastNodeRegistrationRequest request,
            CancellationToken cancellationToken = default
        ) => this.inner.UpsertAsync(request, cancellationToken);

        public Task RemoveAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => this.inner.RemoveAsync(nodeIdentity, cancellationToken);

        public Task<IReadOnlyList<BroadcastNodeRegistration>> GetActiveAsync(
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default
        ) => this.inner.GetActiveAsync(scopes, cancellationToken);

        public Task<BroadcastNodeRegistration> FindAsync(
            string nodeIdentity,
            CancellationToken cancellationToken = default
        ) => this.inner.FindAsync(nodeIdentity, cancellationToken);

        public Task RecordDeliveryAsync(
            string nodeIdentity,
            bool succeeded,
            string failure,
            CancellationToken cancellationToken = default
        ) => this.inner.RecordDeliveryAsync(nodeIdentity, succeeded, failure, cancellationToken);

        public Task RenewLeaseAsync(
            string nodeIdentity,
            DateTimeOffset leaseExpiresUtc,
            CancellationToken cancellationToken = default
        ) => this.inner.RenewLeaseAsync(nodeIdentity, leaseExpiresUtc, cancellationToken);

        public Task ExpireLeasesAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default
        ) => this.inner.ExpireLeasesAsync(utcNow, cancellationToken);

        public Task<IReadOnlyList<BroadcastNodeRegistration>> ListAsync(
            CancellationToken cancellationToken = default
        ) => this.inner.ListAsync(cancellationToken);
    }

    private sealed class CorrelationRecordingHandler : HttpMessageHandler
    {
        public string CapturedCorrelationId { get; private set; }

        public bool HasLegacyCorrelationHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            this.CapturedCorrelationId = request
                .Headers.GetValues(CorrelationId.HeaderName)
                .Single();
            this.HasLegacyCorrelationHeader = request.Headers.Contains("X-Correlation-Id");
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new BroadcastNodeDeliveryResult(
                            "node-b",
                            BroadcastDeliveryOutcome.Accepted
                        )
                    ),
                }
            );
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "TestBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "test")],
                AuthenticationScheme
            );
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                AuthenticationScheme
            );
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

/// <summary>
///     Prevents the Kestrel-based broadcasting tests from competing with other test hosts for runtime resources.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BroadcastingHttpTestCollection
{
    /// <summary>
    ///     Gets the xUnit collection name used by the broadcasting HTTP tests.
    /// </summary>
    public const string Name = "Broadcasting HTTP tests";
}
