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

    [Fact]
    public async Task OnReadyAsync_Action_Calls_OnReady_When_Ready()
    {
        // Arrange
        var readyCalled = false;
        this.service.SetReady();

        // Act
        await this.service.OnReadyAsync(
            onReady: () => readyCalled = true,
            onFaulted: () => throw new Exception("Should not be called"));

        // Assert
        readyCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnReadyAsync_Action_Calls_OnFaulted_When_Faulted()
    {
        // Arrange
        var faultedCalled = false;
        this.service.SetFaulted();

        // Act
        await this.service.OnReadyAsync(
            onReady: () => throw new Exception("Should not be called"),
            onFaulted: () => faultedCalled = true);

        // Assert
        faultedCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnReadyAsync_FuncT_Calls_OnReady_And_Returns_Result()
    {
        // Arrange
        this.service.SetReady();

        // Act
        var result = await this.service.OnReadyAsync(
            onReady: () => 42,
            onFaulted: () => -1);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public async Task OnReadyAsync_FuncT_Calls_OnFaulted_And_Returns_Result()
    {
        // Arrange
        this.service.SetFaulted();

        // Act
        var result = await this.service.OnReadyAsync(
            onReady: () => 42,
            onFaulted: () => -1);

        // Assert
        result.ShouldBe(-1);
    }

    [Fact]
    public async Task OnReadyAsync_AsyncFuncT_Calls_OnReady_And_Returns_Result()
    {
        // Arrange
        this.service.SetReady();

        // Act
        var result = await this.service.OnReadyAsync(
            onReady: async () => { await Task.Delay(10); return 99; },           // Func<Task<int>>
            onFaulted: async () => { await Task.Delay(0); return -1; });          // Func<Task<int>>

        // Assert
        result.ShouldBe(99);
    }

    [Fact]
    public async Task OnReadyAsync_AsyncFuncT_Calls_OnFaulted_And_Returns_Result()
    {
        // Arrange
        this.service.SetFaulted();

        // Act
        var result = await this.service.OnReadyAsync(
            onReady: async () => { await Task.Delay(10); return 99; },
            onFaulted: async () => { await Task.Delay(10); return -1; });

        // Assert
        result.ShouldBe(-1);
    }

    [Fact]
    public async Task OnReadyAsync_TimesOut_Throws_TimeoutException()
    {
        // Arrange & Act & Assert
        var ex = await Should.ThrowAsync<TimeoutException>(() =>
            this.service.OnReadyAsync(
                onReady: () => { },
                onFaulted: null,
                pollInterval: TimeSpan.FromMilliseconds(10),
                timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task OnReadyAsync_NullOnReady_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            this.service.OnReadyAsync(
                onReady: null,
                onFaulted: () => { }));
    }

    [Fact]
    public void Should_IsReady_WithoutName_False_IfNoneReady()
    {
        // No set states
        this.service.IsReady().ShouldBeFalse();

        // One is faulted, none ready
        this.service.SetFaulted("a", "fail");
        this.service.IsReady().ShouldBeFalse();
    }

    [Fact]
    public void Should_IsReady_WithoutName_True_IfAllReady()
    {
        this.service.SetReady("a");
        this.service.SetReady("b");
        this.service.IsReady().ShouldBeTrue();

        this.service.SetFaulted("c", "broken");
        this.service.IsReady().ShouldBeFalse();
    }

    [Fact]
    public void Should_IsReady_WithoutName_False_IfAnyNotReadyOrFaulted()
    {
        this.service.SetReady("a");
        this.service.SetReady("b");
        this.service.SetFaulted("c", "fail");
        this.service.IsReady().ShouldBeFalse();
    }

    [Fact]
    public void Should_IsFaulted_WithoutName_True_IfAnyFaulted()
    {
        this.service.SetReady("a");
        this.service.SetFaulted("b", "fail");
        this.service.IsFaulted().ShouldBeTrue();
    }

    [Fact]
    public void Should_IsFaulted_WithoutName_False_IfNoneFaulted()
    {
        this.service.SetReady("a");
        this.service.SetReady("b");
        this.service.IsFaulted().ShouldBeFalse();
    }

    [Fact]
    public void Should_FaultMessage_WithoutName_Returns_FirstFaultedMessage()
    {
        this.service.SetReady("a");
        this.service.SetFaulted("b", "problem-b");
        this.service.SetFaulted("c", "problem-c");
        this.service.FaultMessage().ShouldBe("problem-b");
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_WithoutName_WaitsForAll()
    {
        // Arrange
        this.service.SetReady("a");
        await Task.Run(async () =>
        {
            await Task.Delay(100);
            this.service.SetReady("b");
        });

        // Act
        await this.service.WaitForReadyAsync(timeout: TimeSpan.FromSeconds(2));

        // Assert
        this.service.IsReady().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_WithoutName_ThrowsOnFaulted()
    {
        // Arrange
        this.service.SetReady("a");
        await Task.Run(async () =>
        {
            await Task.Delay(100);
            this.service.SetFaulted("b", "fail-b");
        });

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            this.service.WaitForReadyAsync(timeout: TimeSpan.FromSeconds(2)));

        ex.Message.ShouldContain("fail-b");
    }

    [Fact]
    public async Task Should_WaitForReadyAsync_WithoutName_ThrowsOnTimeout_WhenNotAllReady()
    {
        // Arrange
        this.service.SetReady("a");
        // "b" never set

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(async () =>
            await this.service.WaitForReadyAsync("b", timeout: TimeSpan.FromMilliseconds(200)));
    }
}