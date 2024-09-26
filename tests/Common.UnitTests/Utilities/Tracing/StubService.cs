// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

public interface IStubService
{
    public void MethodExplicitlyMarkedForTracing(Action stateValidation);

    public Task AsyncMethodExplicitlyMarkedForTracing(Action stateValidation);

    public void MethodNotExplicitlyMarkedForTracing(Action stateValidation);

    public void MethodWithStrangeParams1(
        Action stateValidation,
        IList<string>[] arrayOfList,
        ISet<int[]> setOfArray,
        IDictionary<int, ICollection<string>> dictionary,
        ref int intVal);

    public void MethodJaggedAndMultiDimArraysParams(
        Action stateValidation,
        out string strVal,
        bool[][][] jaggedArrayOfBools,
        short[,,,][,][,,] multiDimArrayOfShorts,
        long[,,][][,][] mixMultiDimAndJaggedArraysOfLongs);
}

public class StubService : IStubService
{
    [TraceActivity]
    public void MethodExplicitlyMarkedForTracing(Action stateValidation)
    {
        stateValidation();
    }

    [TraceActivity]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task AsyncMethodExplicitlyMarkedForTracing(Action stateValidation)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        stateValidation();
    }

    public void MethodNotExplicitlyMarkedForTracing(Action stateValidation)
    {
        stateValidation();
    }

    public void MethodWithStrangeParams1(
        Action stateValidation,
        IList<string>[] arrayOfList,
        ISet<int[]> setOfArray,
        IDictionary<int, ICollection<string>> dict,
        ref int intVal)
    {
        stateValidation();
    }

    public void MethodJaggedAndMultiDimArraysParams(
        Action stateValidation,
        out string strVal,
        bool[][][] jaggedArrayOfBools,
        short[,,,][,][,,] multiDimArrayOfShorts,
        long[,,][][,][] mixMultiDimAndJaggedArraysOfLongs)
    {
        strVal = "hello";
        stateValidation();
    }
}