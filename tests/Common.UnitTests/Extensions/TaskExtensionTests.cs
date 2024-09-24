// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class TaskExtensionsTests
{
    [Fact]
    public void Forget_Success_Succeeds()
    {
        // Arrange
        var task = Task.CompletedTask;

        // Act
        task.Forget();

        // Assert
    }

    [Fact]
    public async Task Forget_Success2_Succeeds()
    {
        // Arrange
        var i = 0;
        var task = this.SuccessMethodAsync();

        // Act
        task.Forget(errorHandler => i++);

        // Assert
        await Task.Delay(300);
        i.ShouldBe(0);
    }

    [Fact]
    public async Task Forget_Failed_Succeeds()
    {
        // Arrange
        var i = 0;
        var task = this.FailedMethodAsync();

        // Act
        task.Forget(errorHandler => i++);

        // Assert
        await Task.Delay(300);
        i.ShouldBe(1);
    }

    [Fact]
    public async Task RetryWithResult_Success_Succeeds()
    {
        // Arrange
        var taskFactory = () => Task.FromResult("Success");

        // Act
        //string result = await Should.NotThrowAsync(() => taskFactory.Retry(retryCount, delay));
        var result = await taskFactory.Retry(3, TimeSpan.FromMilliseconds(100));

        // Assert
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task RetryNoResult_Fails_Failed()
    {
        // Arrange
        var task = this.FailedMethodAsync();

        // Act
        var result = await Should.ThrowAsync<Exception>(() => task.Retry(3, TimeSpan.FromMilliseconds(100)));

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe("Failed");
    }

    [Fact]
    public async Task RetryWithResult_Fails_Failed()
    {
        // Arrange
        var i = 0;
        Func<Task<string>> taskFactory = async () =>
        {
            i++;
            await Task.Delay(100); // Simulate some work
            throw new Exception("Failed");
        };

        // Act
        var result = await Should.ThrowAsync<Exception>(() => taskFactory.Retry(3, TimeSpan.FromMilliseconds(100)));

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe("Failed");
    }

    [Fact]
    public async Task OnFailure_Success_Succeeds()
    {
        // Arrange
        var i = 0;
        var task = this.SuccessMethodAsync();

        // Act
        await Should.NotThrowAsync(() => task.OnFailure(ex => i++));

        // Assert
        await Task.Delay(300);
        i.ShouldBe(0);
    }

    [Fact]
    public async Task OnFailure_Fail_CallOnFailure()
    {
        // Arrange
        var i = 0;
        var task = this.FailedMethodAsync();

        // Act
        await Should.NotThrowAsync(() => task.OnFailure(ex => i++));

        // Assert
        await Task.Delay(300);
        i.ShouldBe(1);
    }

    [Fact]
    public async Task WithTimeout_LongerThanTask_Succeeds()
    {
        // Arrange
        var task = Task.Delay(TimeSpan.FromMilliseconds(300));

        // Act
        await Should.NotThrowAsync(() => task.WithTimeout(TimeSpan.FromMilliseconds(500)));

        // Assert
    }

    [Fact]
    public async Task WithTimeout_ShortThanTask_ThrowsException()
    {
        // Arrange
        var task = Task.Delay(TimeSpan.FromMilliseconds(300));

        // Act
        await Should.ThrowAsync<TimeoutException>(() => task.WithTimeout(TimeSpan.FromMilliseconds(100)));

        // Assert
    }

    [Fact]
    public async Task Fallback_Success_Succeeds()
    {
        // Arrange
        var task = Task.FromResult("Success");
        var fallbackValue = "Fallback";

        // Act
        var result = await task.Fallback(fallbackValue);

        // Assert
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task AnyContext_Success_Succeeds()
    {
        // Arrange
        //var task = Task.Delay(TimeSpan.FromSeconds(1));
        var task = Task.CompletedTask;

        // Act
        await task.AnyContext();

        // Assert
    }

    private async Task SuccessMethodAsync()
    {
        await Task.Delay(100); // Simulate some work
    }

    private async Task FailedMethodAsync()
    {
        await Task.Delay(100); // Simulate some work

        throw new Exception("Failed");
    }
}