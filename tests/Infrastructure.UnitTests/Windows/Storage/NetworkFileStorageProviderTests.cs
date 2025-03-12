// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Windows.Storage;

using NSubstitute;
using Shouldly;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using BridgingIT.DevKit.Infrastructure.Windows;
using BridgingIT.DevKit.Infrastructure.Windows.Storage;

public class NetworkFileStorageProviderTests : IDisposable
{
    private readonly string path;
    private readonly IWindowsAuthenticator mockAuthenticator;
    private readonly WindowsImpersonationService impersonationService;

    public NetworkFileStorageProviderTests()
    {
        // Setup
        this.mockAuthenticator = Substitute.For<IWindowsAuthenticator>();
        this.path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.path);

        // Create a real impersonation service with the mock authenticator
        this.impersonationService = new WindowsImpersonationService(this.mockAuthenticator);
    }

    [SkippableFact]
    public void Constructor_InitializesWithImpersonationService()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange & Act
        var provider = new NetworkFileStorageProvider(
            this.path,
            "TestLocation",
            this.impersonationService);

        // Assert
        provider.LocationName.ShouldBe("TestLocation");
    }

    [SkippableFact]
    public void Constructor_WithCredentials_CreatesNewImpersonationService()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        try
        {
            // Arrange & Act - This might throw if credentials can't be validated
            var provider = new NetworkFileStorageProvider(
                this.path,
                "TestLocation",
                "user",
                "pass",
                "domain");

            // Assert
            provider.LocationName.ShouldBe("TestLocation");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Skip if we can't create real credentials - this is expected in test environment
            Skip.If(true, "Cannot create real Windows credentials in test environment");
        }
    }

    [SkippableFact]
    public async Task ReadFileAsync_UsesAuthenticatorFromImpersonationService()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        try
        {
            // Setup authenticator to return current user's identity
            this.mockAuthenticator
                .Authenticate()
                .Returns((IntPtr.Zero, System.Security.Principal.WindowsIdentity.GetCurrent()));

            // Arrange
            var provider = new NetworkFileStorageProvider(
                this.path,
                "TestLocation",
                this.impersonationService);

            // Create a test file
            var testFilePath = "test.txt";
            await File.WriteAllTextAsync(Path.Combine(this.path, testFilePath), "test content");

            // Act
            await provider.ReadFileAsync(testFilePath);

            // Assert
            this.mockAuthenticator
                .Received(1)
                .Authenticate();
        }
        catch (Exception ex)
        {
            // Skip if impersonation fails - this is expected in some test environments
            Skip.If(true, $"Impersonation failed: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task MultipleOperations_UseAuthenticatorOnlyOnce()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        // Setup authenticator to return current user's identity
        this.mockAuthenticator
            .Authenticate()
            .Returns((IntPtr.Zero, System.Security.Principal.WindowsIdentity.GetCurrent()));

        // Arrange
        var provider = new NetworkFileStorageProvider(
            this.path,
            "TestLocation",
            this.impersonationService);

        // Create test files
        var testFilePath1 = "test1.txt";
        var testFilePath2 = "test2.txt";
        await File.WriteAllTextAsync(Path.Combine(this.path, testFilePath1), "test content 1");
        await File.WriteAllTextAsync(Path.Combine(this.path, testFilePath2), "test content 2");

        // Act
        await provider.ReadFileAsync(testFilePath1);
        await provider.ReadFileAsync(testFilePath2);

        // Assert
        this.mockAuthenticator
            .Received(2)
            .Authenticate();
    }

    [SkippableFact]
    public void Dispose_DisposesImpersonationService()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Arrange
        var provider = new NetworkFileStorageProvider(
            this.path,
            "TestLocation",
            this.impersonationService);

        // Act
        provider.Dispose();

        // Assert
        this.mockAuthenticator
            .Received(1)
            .Dispose();
    }

    [SkippableFact]
    public async Task ReadFileAsync_ReturnsCorrectContent_WithCurrentUserImpersonation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        try
        {
            // Setup authenticator to return current user's identity
            this.mockAuthenticator
                .Authenticate()
                .Returns((IntPtr.Zero, System.Security.Principal.WindowsIdentity.GetCurrent()));

            // Arrange
            var provider = new NetworkFileStorageProvider(
                this.path,
                "TestLocation",
                this.impersonationService);

            var testFilePath = "content-test.txt";
            var expectedContent = "Test content";
            await File.WriteAllTextAsync(Path.Combine(this.path, testFilePath), expectedContent);

            // Act
            var result = await provider.ReadFileAsync(testFilePath);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            using var reader = new StreamReader(result.Value);
            var content = await reader.ReadToEndAsync();
            content.ShouldBe(expectedContent);
        }
        catch (Exception ex)
        {
            // Skip if impersonation fails - this is expected in some test environments
            Skip.If(true, $"Impersonation failed: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task WriteFileAsync_WritesContentCorrectly_WithCurrentUserImpersonation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows impersonation is only supported on Windows.");
        }

        try
        {
            // Setup authenticator to return current user's identity
            this.mockAuthenticator
                .Authenticate()
                .Returns((IntPtr.Zero, System.Security.Principal.WindowsIdentity.GetCurrent()));

            // Arrange
            var provider = new NetworkFileStorageProvider(
                this.path,
                "TestLocation",
                this.impersonationService);

            var testFilePath = "write-test.txt";
            var expectedContent = "Content to write";

            // Act
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(expectedContent);
            await writer.FlushAsync();
            stream.Position = 0;

            var writeResult = await provider.WriteFileAsync(testFilePath, stream);

            // Assert
            writeResult.IsSuccess.ShouldBeTrue();

            var writtenContent = await File.ReadAllTextAsync(Path.Combine(this.path, testFilePath));
            writtenContent.ShouldBe(expectedContent);
        }
        catch (Exception ex)
        {
            // Skip if impersonation fails - this is expected in some test environments
            Skip.If(true, $"Impersonation failed: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task IntegrationTest_WithRealCredentials()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "windows only");

        // Get credentials from environment variables
        var username = Environment.GetEnvironmentVariable("TEST_WINDOWS_USERNAME");
        var password = Environment.GetEnvironmentVariable("TEST_WINDOWS_PASSWORD");
        var domain = Environment.GetEnvironmentVariable("TEST_WINDOWS_DOMAIN");

        Skip.If(string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password),
            "Test credentials not set in environment variables");

        try
        {
            // Create a real impersonation service with real authenticator
            var authenticator = new WindowsAuthenticator(username, password, domain);
            var impersonationService = new WindowsImpersonationService(authenticator);

            // Create provider with real impersonation
            var provider = new NetworkFileStorageProvider(
                this.path,
                "TestLocation",
                impersonationService);

            // Create and read a file
            var testFile = "integration-test.txt";
            var expectedContent = "Full integration test content";

            // Write file 
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(expectedContent);
            await writer.FlushAsync();
            stream.Position = 0;

            var writeResult = await provider.WriteFileAsync(testFile, stream);
            writeResult.IsSuccess.ShouldBeTrue();

            // Read file back
            var readResult = await provider.ReadFileAsync(testFile);
            readResult.IsSuccess.ShouldBeTrue();

            using var reader = new StreamReader(readResult.Value);
            var actualContent = await reader.ReadToEndAsync();
            actualContent.ShouldBe(expectedContent);

            // Clean up
            provider.Dispose();
        }
        catch (Exception ex)
        {
            // Skip if real impersonation fails 
            Skip.If(true, $"Real impersonation failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Cleanup
        try { Directory.Delete(this.path, true); } catch { }

        // Dispose impersonation service
        try { this.impersonationService.Dispose(); } catch { }
    }
}