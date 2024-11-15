// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using System.Text.Json;
using NSubstitute.ExceptionExtensions;

[UnitTest("Common")]
public class StreamExtensionsTests
{
    [Fact]
    public void ReadToEndTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5)
            .ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var result = stream.ReadToEnd();

        result.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task ReadToEndAsyncTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5)
            .ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var result = await stream.ReadToEndAsync();

        result.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public void TryReadAllTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5)
            .ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = new byte[5];
        stream.TryReadAll(buffer, 0, 5);

        buffer.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task TryReadAllAsyncTests()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5)
            .ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = new byte[5];
        await stream.TryReadAllAsync(buffer, 0, 5);

        buffer.ShouldBe([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task ToMemoryStreamAsyncTest()
    {
        using var stream = new MemoryStream();
        Enumerable.Range(0, 5)
            .ForEach(i => stream.WriteByte((byte)i));
        stream.Seek(1, SeekOrigin.Begin);

        using var copy = await stream.ToMemoryStreamAsync();

        copy.ToArray()
            .ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void ToStream_WithValidString_ShouldReturnStreamWithCorrectContent()
    {
        // Arrange
        const string testString = "Hello World";

        // Act
        using var stream = testString.ToStream();
        using var reader = new StreamReader(stream);
        var result = reader.ReadToEnd();

        // Assert
        result.ShouldBe(testString);
    }

    [Fact]
    public void ToStream_WithEmptyString_ShouldReturnEmptyStream()
    {
        // Arrange
        const string testString = "";

        // Act
        using var stream = testString.ToStream();

        // Assert
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public void ToStream_WithNullString_ShouldThrowArgumentNullException()
    {
        // Arrange
        string testString = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => testString.ToStream());
    }

    [Fact]
    public void ToStream_WithCustomEncoding_ShouldUseSpecifiedEncoding()
    {
        // Arrange
        const string testString = "Hello World";
        var encoding = Encoding.ASCII;

        // Act
        using var stream = testString.ToStream(encoding);
        using var reader = new StreamReader(stream, encoding);
        var result = reader.ReadToEnd();

        // Assert
        result.ShouldBe(testString);
    }

    private class TestClass
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    [Fact]
    public void ToStream_WithValidObject_ShouldSerializeCorrectly()
    {
        // Arrange
        var testObject = new TestClass { Name = "Test", Age = 30 };

        // Act
        using var stream = testObject.ToStream();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var deserializedObject = JsonSerializer.Deserialize<TestClass>(json);

        // Assert
        deserializedObject.ShouldNotBeNull();
        deserializedObject.Name.ShouldBe(testObject.Name);
        deserializedObject.Age.ShouldBe(testObject.Age);
    }

    [Fact]
    public void ToStream_WithCustomOptions_ShouldUseSpecifiedOptions()
    {
        // Arrange
        var testObject = new TestClass { Name = "Test", Age = 30 };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        // Act
        using var stream = testObject.ToStream(options);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        // Assert
        json.ShouldContain("\n"); // Verifying indentation
    }

    [Fact]
    public async Task ToStreamAsync_WithValidObject_ShouldSerializeCorrectly()
    {
        // Arrange
        var testObject = new TestClass { Name = "Test", Age = 30 };

        // Act
        using var stream = await testObject.ToStreamAsync();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var deserializedObject = JsonSerializer.Deserialize<TestClass>(json);

        // Assert
        deserializedObject.ShouldNotBeNull();
        deserializedObject.Name.ShouldBe(testObject.Name);
        deserializedObject.Age.ShouldBe(testObject.Age);
    }

    [Fact]
    public void ToStream_WithByteArray_ShouldReturnCorrectStream()
    {
        // Arrange
        var testBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        using var stream = testBytes.ToStream();
        var resultBytes = new byte[testBytes.Length];
        stream.Read(resultBytes, 0, resultBytes.Length);

        // Assert
        resultBytes.ShouldBe(testBytes);
    }

    [Fact]
    public void ToStream_WithStream_ShouldCreateNewStream()
    {
        // Arrange
        var testBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        using var resultStream = testBytes.ToStream();
        var resultBytes = new byte[testBytes.Length];
        resultStream.Read(resultBytes, 0, resultBytes.Length);

        // Assert
        resultBytes.ShouldBe(testBytes);
    }

    [Fact]
    public void ToStream_WithCustomBufferSize_ShouldUseSpecifiedBuffer()
    {
        // Arrange
        const int bufferSize = 1024;
        var testBytes = new byte[bufferSize * 2];
        new Random().NextBytes(testBytes);
        using var sourceStream = new MemoryStream(testBytes);

        // Act
        using var resultStream = sourceStream.ToStream(bufferSize);
        var resultBytes = new byte[testBytes.Length];
        resultStream.Read(resultBytes, 0, resultBytes.Length);

        // Assert
        resultBytes.ShouldBe(testBytes);
    }

    [Fact]
    public async Task ToStreamAsync_WithStream_ShouldCreateNewStream()
    {
        // Arrange
        var testBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await using var resultStream = await testBytes.ToStreamAsync();
        var resultBytes = new byte[testBytes.Length];
        await resultStream.ReadAsync(resultBytes);

        // Assert
        resultBytes.ShouldBe(testBytes);
    }

    [Fact]
    public async Task ToStreamAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var testBytes = new byte[81920 * 2];
        new Random().NextBytes(testBytes);
        await using var sourceStream = new MemoryStream(testBytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            sourceStream.ToStreamAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ToStreamAsync_WithCustomBufferSize_ShouldUseSpecifiedBuffer()
    {
        // Arrange
        const int bufferSize = 1024;
        var testBytes = new byte[bufferSize * 2];
        new Random().NextBytes(testBytes);
        await using var sourceStream = new MemoryStream(testBytes);

        // Act
        await using var resultStream = await sourceStream.ToStreamAsync(bufferSize);
        var resultBytes = new byte[testBytes.Length];
        await resultStream.ReadAsync(resultBytes);

        // Assert
        resultBytes.ShouldBe(testBytes);
    }
}