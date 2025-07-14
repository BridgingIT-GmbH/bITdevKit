// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<RequesterBenchmarks>();
        //BenchmarkRunner.Run<MediatRBenchmarks>();
        BenchmarkRunner.Run<NotifierBenchmarks>();
        //BenchmarkRunner.Run<ResultBenchmarks>();
        //BenchmarkRunner.Run<ResultNonGenericBenchmarks>();
    }
}