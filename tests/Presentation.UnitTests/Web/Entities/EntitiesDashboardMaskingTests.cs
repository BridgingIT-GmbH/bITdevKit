// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Text.Json;
using System.Text.Json.Nodes;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

[UnitTest("Presentation")]
public class EntitiesDashboardMaskingTests
{
    private static readonly Action<EntitiesDashboardOptions> Configure = o =>
        o.Entity<Customer>(e => e
            .Mask(c => c.ApiToken, "***")
            .Hide(c => c.InternalNote)
            .Mask(c => c.Address.TaxNumber, "<tax>")
            .Hide(c => c.Address.Street));

    private readonly EntitiesDashboardEntity entity = EntitiesDashboardTestData.DescribeCustomer(Configure);

    private readonly Customer customer = EntitiesDashboardTestData.NewCustomer(EntitiesDashboardTestData.CustomerGuid1, "Ann", 41);

    [Fact]
    public void Project_MaskedProperty_ReturnsMaskTextAndNeverTheStoredValue()
    {
        // Arrange & Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        row["ApiToken"].ShouldBe("***");
        JsonSerializer.Serialize(row).ShouldNotContain("secret-token");
    }

    [Fact]
    public void Project_HiddenProperty_IsOmittedFromTheRow()
    {
        // Arrange & Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        row.ContainsKey("InternalNote").ShouldBeFalse();
        JsonSerializer.Serialize(row).ShouldNotContain("internal");
    }

    [Fact]
    public void Project_ScalarValues_AreFormattedByKind()
    {
        // Arrange
        this.customer.BirthDate = new DateTime(1980, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        this.customer.Status = CustomerStatus.Suspended;

        // Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        row["Name"].ShouldBe("Ann");
        row["Age"].ShouldBe(41);
        row["IsVip"].ShouldBe(false);
        row["Id"].ShouldBe(EntitiesDashboardTestData.CustomerGuid1.ToString());
        row["Status"].ShouldBe("Suspended");
        row["Tier"].ShouldBe("1");
        ((string)row["BirthDate"]).ShouldStartWith("1980-05-06");
    }

    [Fact]
    public void Project_NullValue_StaysNull()
    {
        // Arrange
        this.customer.BirthDate = null;
        this.customer.Address = null;

        // Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        row["BirthDate"].ShouldBeNull();
        row["Address"].ShouldBeNull();
    }

    [Fact]
    public void Project_ValueObject_MasksAndRemovesMembersInsideTheJson()
    {
        // Arrange & Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        var json = JsonNode.Parse((string)row["Address"]).AsObject();
        json["city"].ToString().ShouldBe("Springfield");
        json["taxNumber"].ToString().ShouldBe("<tax>");
        json.ContainsKey("street").ShouldBeFalse();
        ((string)row["Address"]).ShouldNotContain("TAX-1");
        ((string)row["Address"]).ShouldNotContain("Main Street");
    }

    [Fact]
    public void Project_MaskedAndNullValue_StillReturnsMaskText()
    {
        // Arrange
        this.customer.ApiToken = null;

        // Act
        var row = EntitiesDashboardMasking.Project(this.entity, this.customer);

        // Assert
        row["ApiToken"].ShouldBe("***");
    }

    [Fact]
    public void GetVisibleProperties_ExcludesHiddenProperties()
    {
        // Arrange & Act
        var visible = EntitiesDashboardMasking.GetVisibleProperties(this.entity);

        // Assert
        visible.ShouldNotContain(p => p.Name == "InternalNote");
        visible.ShouldContain(p => p.Name == "ApiToken");
    }

    [Fact]
    public void MergeValueObjectJson_SubmittedMaskedAndHiddenMembers_AreRestoredFromTheStoredValue()
    {
        // Arrange
        var property = this.entity.FindProperty("Address");
        var submitted = """{"city":"Shelbyville","taxNumber":"<tax>","street":"hacked"}""";

        // Act
        var merged = JsonNode.Parse(EntitiesDashboardMasking.MergeValueObjectJson(property, submitted, this.customer.Address, null)).AsObject();

        // Assert
        merged["city"].ToString().ShouldBe("Shelbyville");
        merged["taxNumber"].ToString().ShouldBe("TAX-1");
        merged["street"].ToString().ShouldBe("Main Street 1");
    }

    [Fact]
    public void MergeValueObjectJson_ReplacementForMaskedMember_OverridesTheStoredValue()
    {
        // Arrange
        var property = this.entity.FindProperty("Address");
        var submitted = """{"city":"Springfield","taxNumber":"<tax>"}""";
        var replacements = new Dictionary<string, string> { ["Address.TaxNumber"] = "TAX-NEW" };

        // Act
        var merged = JsonNode.Parse(EntitiesDashboardMasking.MergeValueObjectJson(property, submitted, this.customer.Address, replacements)).AsObject();

        // Assert
        merged["taxNumber"].ToString().ShouldBe("TAX-NEW");
        merged["street"].ToString().ShouldBe("Main Street 1");
    }

    [Fact]
    public void MergeValueObjectJson_NoStoredValue_DropsMaskedAndHiddenMembers()
    {
        // Arrange
        var property = this.entity.FindProperty("Address");
        var submitted = """{"city":"Springfield","taxNumber":"<tax>","street":"x"}""";

        // Act
        var merged = JsonNode.Parse(EntitiesDashboardMasking.MergeValueObjectJson(property, submitted, null, null)).AsObject();

        // Assert
        merged.ContainsKey("taxNumber").ShouldBeFalse();
        merged.ContainsKey("street").ShouldBeFalse();
        merged["city"].ToString().ShouldBe("Springfield");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void MergeValueObjectJson_BlankOrNonObjectInput_IsReturnedUnchanged(string submitted)
    {
        // Arrange
        var property = this.entity.FindProperty("Address");

        // Act
        var merged = EntitiesDashboardMasking.MergeValueObjectJson(property, submitted, this.customer.Address, null);

        // Assert
        merged.ShouldBe(submitted);
    }
}
