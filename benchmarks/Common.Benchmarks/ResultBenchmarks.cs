// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;

[MemoryDiagnoser]
public class ResultBenchmarks
{
    private static readonly string TestMessage = "Test message";
    private static readonly ValidationError TestError = new("Test error");
    private static readonly User TestUser = new() { Id = 1, Name = "John" };

    [Benchmark]
    public Result<User> Result_CreateSuccessResult()
    {
        return Result<User>.Success(TestUser);
    }

    [Benchmark]
    public Result<User> Result_CreateFailureResult()
    {
        return Result<User>.Failure().WithError(TestError);
    }

    [Benchmark]
    public Result<User> Result_AddMessageToResult()
    {
        var result = Result<User>.Success(TestUser);
        return result.WithMessage(TestMessage);
    }

    [Benchmark]
    public bool Result_CheckIsSuccess()
    {
        var result = Result<User>.Success(TestUser);
        return result.IsSuccess;
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
