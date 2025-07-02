// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Windows.Storage;

using NSubstitute;
using Shouldly;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using BridgingIT.DevKit.Infrastructure.Windows;
using System.Security.Principal;

public class WindowsImpersonationServiceTests
{
    private readonly IWindowsAuthenticator authenticator;
    private readonly IntPtr fakeToken;
    private readonly WindowsIdentity fakeIdentity;

    public WindowsImpersonationServiceTests()
    {
        // Setup
        this.authenticator = Substitute.For<IWindowsAuthenticator>();

        // Mock WindowsIdentity if on Windows, otherwise use null for tests
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            this.fakeToken = new IntPtr(42); // Fake token
            this.fakeIdentity = WindowsIdentity.GetCurrent(); // Use current for testing

            // Setup the authenticator mock
            this.authenticator
                .Authenticate()
                .Returns((this.fakeToken, this.fakeIdentity));

            this.authenticator
                .CloseToken(Arg.Any<IntPtr>())
                .Returns(true);
        }
    }

    [SkippableFact]
    public void Constructor_InitializesWithAuthenticator()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange & Act
        var service = new WindowsImpersonationService(this.authenticator);

        // Assert - No exception means success
        service.ShouldNotBeNull();
    }

    [SkippableFact]
    public void Constructor_ThrowsOnNullAuthenticator()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new WindowsImpersonationService(null));
    }

    [SkippableFact]
    public async Task ExecuteImpersonatedAsync_Generic_CallsAuthenticateAndExecutesOperation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange
        var service = new WindowsImpersonationService(this.authenticator);
        var operationCalled = false;

        // Act
        var result = await service.ExecuteImpersonatedAsync(async () =>
        {
            operationCalled = true;
            return await Task.FromResult(42);
        });

        // Assert
        this.authenticator.Received(1).Authenticate();
        operationCalled.ShouldBeTrue();
        result.ShouldBe(42);
    }

    [SkippableFact]
    public async Task ExecuteImpersonatedAsync_NonGeneric_CallsAuthenticateAndExecutesOperation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange
        var service = new WindowsImpersonationService(this.authenticator);
        var operationCalled = false;

        // Act
        await service.ExecuteImpersonatedAsync(async () =>
        {
            operationCalled = true;
            await Task.CompletedTask;
        });

        // Assert
        this.authenticator.Received(1).Authenticate();
        operationCalled.ShouldBeTrue();
    }

    [SkippableFact]
    public void Dispose_DisposesAuthenticator()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange
        var service = new WindowsImpersonationService(this.authenticator);

        // Act
        service.Dispose();

        // Assert
        this.authenticator.Received(1).Dispose();
    }

    [SkippableFact]
    public void ConstructorWithCredentials_CreatesAuthenticator()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        try
        {
            // This test is more of an integration test since it creates a real WindowsAuthenticator
            // Arrange & Act
            var service = new WindowsImpersonationService("username", "password", "domain");

            // Assert
            service.ShouldNotBeNull();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Skip if we can't create real credentials - this is expected in test environment
            Skip.If(true, "Cannot create real Windows credentials in test environment");
        }
    }
}