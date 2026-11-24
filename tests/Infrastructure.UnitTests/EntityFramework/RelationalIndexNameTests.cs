// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Checks stable bounded relational identifiers and cache model compatibility.</summary>
public sealed class RelationalIndexNameTests
{
    [Fact]
    public void Create_ShortName_PreservesTableAndColumnOrder()
    {
        RelationalIndexName.Create("Products", "TenantId", "Code").ShouldBe("IX_Products_TenantId_Code");
    }

    [Fact]
    public void Create_LongCacheName_PreservesExistingModelIdentifier()
    {
        var columns = new[] { "CollectionId", "ContractId", "HasForegroundAccess", "AbsoluteExpiryTicks", "Id" };
        var name = RelationalIndexName.Create("__Caching_Entries", columns);

        name.ShouldBe("IX___Caching_Entries_CollectionId_ContractId_HasFo_7d9c3f4ac379");
        CacheIndexName.Create("__Caching_Entries", columns).ShouldBe(name);
    }

    [Fact]
    public void Create_ProviderLimit_BoundsLengthAndDistinguishesLongNames()
    {
        var table = new string('A', 100);
        var first = RelationalIndexName.Create(table, 32, "First");
        var second = RelationalIndexName.Create(table, 32, "Second");

        first.Length.ShouldBe(32);
        second.Length.ShouldBe(32);
        first.ShouldNotBe(second);
        RelationalIndexName.Create(table, 128, "First").ShouldBe("IX_" + table + "_First");
        Should.Throw<ArgumentOutOfRangeException>(() => RelationalIndexName.Create(table, 13, "First"));
    }
}