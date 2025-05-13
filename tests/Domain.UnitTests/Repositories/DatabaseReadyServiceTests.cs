// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Repositories;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using Shouldly;
using Xunit;

public class DatabaseReadyServiceTests
{
    private readonly DatabaseReadyService service = new();

    [Fact]
    public void Should_Initially_NotBeReadyOrFaulted()
    {
        // arrange, act
        var isReady = this.service.IsReady();
        var isFaulted = this.service.IsFaulted();
        var faultMsg = this.service.FaultMessage();

        // assert
        isReady.ShouldBeFalse();
        isFaulted.ShouldBeFalse();
        faultMsg.ShouldBeNull();
    }

    [Fact]
    public void Should_SetReady_And_ReportReady()
    {
        // arrange, act
        this.service.SetReady();

        // assert
        this.service.IsReady().ShouldBeTrue();
        this.service.IsFaulted().ShouldBeFalse();
        this.service.FaultMessage().ShouldBeNull();
    }

    [Fact]
    public void Should_SetFaulted_And_ReportFaulted()
    {
        // arrange, act
        this.service.SetFaulted(message: "db error");

        // assert
        this.service.IsFaulted().ShouldBeTrue();
        this.service.IsReady().ShouldBeFalse();
        this.service.FaultMessage().ShouldBe("db error");
    }

    [Fact]
    public void Should_AllowMultipleDatabases_ByName()
    {
        // arrange
        this.service.SetReady("mod1");
        this.service.SetFaulted("mod2", "broken");

        // act & assert
        this.service.IsReady("mod1").ShouldBeTrue();
        this.service.IsFaulted("mod1").ShouldBeFalse();

        this.service.IsReady("mod2").ShouldBeFalse();
        this.service.IsFaulted("mod2").ShouldBeTrue();
        this.service.FaultMessage("mod2").ShouldBe("broken");
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_Succeeds_WhenReady()
    {
        // arrange
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Task.Run(async () =>
        {
            await Task.Delay(100, cancellationTokenSource.Token);
            this.service.SetReady();
        }, cancellationTokenSource.Token);

        // act
        await this.service.WaitForReadyAsync(timeout: TimeSpan.FromSeconds(2), cancellationToken: cancellationTokenSource.Token);

        // assert
        this.service.IsReady().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_Fails_OnTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromMilliseconds(200);

        // act & assert
        await Should.ThrowAsync<TimeoutException>(async () =>
            await this.service.WaitForReadyAsync(timeout: timeout));
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_Fails_OnFaulted()
    {
        // arrange
        await Task.Run(async () =>
        {
            await Task.Delay(100);
            this.service.SetFaulted(message: "init failed");
        });

        // act & assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await this.service.WaitForReadyAsync(timeout: TimeSpan.FromSeconds(2)));

        ex.Message.ShouldContain("init failed");
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_ByName()
    {
        // arrange
        const string dbName = "moduleX";
        await Task.Run(async () =>
        {
            await Task.Delay(100);
            this.service.SetReady(dbName);
        });

        // act
        await this.service.WaitForReadyAsync(dbName, timeout: TimeSpan.FromSeconds(2));

        // assert
        this.service.IsReady(dbName).ShouldBeTrue();
    }
}