// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Mcp;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public sealed class McpDashboardStatusTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"bdk-mcp-status-{Guid.NewGuid():N}");

    [Fact]
    public void Get_WhenSessionReaderIsNotRegistered_ReturnsOffline()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();

        // Act
        var sut = McpDashboardStatus.Get(provider);

        // Assert
        sut.Status.ShouldBe("offline");
        sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void Get_WhenNoServerSessionExists_ReturnsOffline()
    {
        // Arrange
        using var provider = this.CreateProvider(out _);

        // Act
        var sut = McpDashboardStatus.Get(provider);

        // Assert
        sut.Status.ShouldBe("offline");
        sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void Get_WhenServerTargetsCurrentRuntime_ReturnsConnected()
    {
        // Arrange
        using var provider = this.CreateProvider(out var reader);
        this.WriteSession(reader, "test-runtime");

        // Act
        var sut = McpDashboardStatus.Get(provider);

        // Assert
        sut.Status.ShouldBe("connected");
        sut.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void Get_WhenServerTargetsOtherRuntime_ReturnsRunningWithoutConnection()
    {
        // Arrange
        using var provider = this.CreateProvider(out var reader);
        this.WriteSession(reader, "other-runtime");

        // Act
        var sut = McpDashboardStatus.Get(provider);

        // Assert
        sut.Status.ShouldBe("running");
        sut.IsConnected.ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(this.rootPath))
        {
            Directory.Delete(this.rootPath, true);
        }
    }

    private ServiceProvider CreateProvider(out McpServerSessionReader reader)
    {
        var registryPath = Path.Combine(this.rootPath, "hosts", "runtime");
        Directory.CreateDirectory(registryPath);
        var services = new ServiceCollection();
        services.AddSingleton(new HostDescriptorOptions
        {
            RegistryPath = registryPath,
            RuntimeId = "test-runtime",
            WorkspacePath = this.rootPath
        });
        services.AddSingleton<McpServerSessionReader>();
        var provider = services.BuildServiceProvider();
        reader = provider.GetRequiredService<McpServerSessionReader>();

        return provider;
    }

    private void WriteSession(McpServerSessionReader reader, string selectedRuntimeId)
    {
        var snapshot = reader.GetSnapshot();
        Directory.CreateDirectory(snapshot.SessionDirectory);
        var descriptor = new McpServerSessionDescriptor
        {
            SessionId = Guid.NewGuid().ToString("N"),
            WorkspaceHash = snapshot.WorkspaceHash,
            SelectedRuntimeId = selectedRuntimeId,
            StartedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            Status = "running"
        };

        File.WriteAllText(
            Path.Combine(snapshot.SessionDirectory, $"{descriptor.SessionId}.json"),
            JsonSerializer.Serialize(descriptor));
    }
}
