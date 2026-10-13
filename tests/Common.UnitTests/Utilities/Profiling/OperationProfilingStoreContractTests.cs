// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using BridgingIT.DevKit.Tests.Profiling;

public sealed class OperationProfilingStoreContractTests : ProfilingStorageContractTestsBase
{
    protected override Task<IProfilingStorageProvider> CreateProviderAsync(ProfilingOptions options, TimeProvider clock) =>
        Task.FromResult<IProfilingStorageProvider>(new InMemoryProfilingStorageProvider(options, clock));
}
