// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.Threading;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))]
public class FileStorageProviderTreeTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    private IFileStorageProvider CreateInMemoryProvider()
    {
        return new LoggingFileStorageBehavior(
            new InMemoryFileStorageProvider("InMemory"),
            this.fixture.ServiceProvider.GetRequiredService<ILoggerFactory>());
    }

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_FullStructure_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_FullStructure_Success(this.CreateInMemoryProvider());

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_SubPath_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_SubPath_Success(this.CreateInMemoryProvider());

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_EmptyStructure_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_EmptyStructure_Success(this.CreateInMemoryProvider());

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_SkipFiles_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_SkipFiles_Success(this.CreateInMemoryProvider());

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_ProgressReporting()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_ProgressReporting(this.CreateInMemoryProvider());

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_HtmlRenderer_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_HtmlRenderer_Success(this.CreateInMemoryProvider());
}
