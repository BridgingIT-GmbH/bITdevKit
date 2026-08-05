// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

public class DatabaseReadyServiceContractTests
{
    [Fact]
    public void Contract_IsOwnedByCommonAbstractions()
    {
        // Act
        var assemblyName = typeof(IDatabaseReadyService).Assembly.GetName().Name;

        // Assert
        assemblyName.ShouldBe("BridgingIT.DevKit.Common.Abstractions");
    }
}