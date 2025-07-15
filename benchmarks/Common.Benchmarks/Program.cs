// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Running;

// dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj
public static class Program
{
    public static void Main(string[] args)
    {
        //BenchmarkRunner.Run<MediatRBenchmarks>();
        //BenchmarkRunner.Run<RequesterBenchmarks>();
        BenchmarkRunner.Run<SimpleRequesterBenchmarks>();
        //BenchmarkRunner.Run<NotifierBenchmarks>();
        BenchmarkRunner.Run<SimpleNotifierBenchmarks>();
        //BenchmarkRunner.Run<ResultBenchmarks>();
        //BenchmarkRunner.Run<ResultNonGenericBenchmarks>();
    }
}