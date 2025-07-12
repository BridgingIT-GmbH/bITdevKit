// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;

[MemoryDiagnoser]
public class ResultNonGenericBenchmarks
{
    private static readonly string TestMessage = "Test message";
    private static readonly ValidationError TestError = new("Test error");

    [Benchmark]
    public Result ResultNonGeneric_CreateSuccessResult()
    {
        return Result.Success();
    }

    [Benchmark]
    public Result ResultNonGeneric_CreateFailureResult()
    {
        return Result.Failure().WithError(TestError);
    }

    [Benchmark]
    public Result ResultNonGeneric_AddMessageToResult()
    {
        var result = Result.Success();
        return result.WithMessage(TestMessage);
    }

    [Benchmark]
    public bool ResultNonGeneric_CheckIsSuccess()
    {
        var result = Result.Success();
        return result.IsSuccess;
    }
}
