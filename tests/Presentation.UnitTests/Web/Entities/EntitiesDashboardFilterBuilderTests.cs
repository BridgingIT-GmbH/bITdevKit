// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

[UnitTest("Presentation")]
public class EntitiesDashboardFilterBuilderTests
{
    private readonly EntitiesDashboardEntity customer = EntitiesDashboardTestData.DescribeCustomer(o =>
    {
        o.Entity<Customer>(e => e.Mask(c => c.ApiToken).Hide(c => c.InternalNote));
    });

    [Fact]
    public void ParseConditions_ValidCondition_ReturnsLowercaseOperatorAndValue()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["name~CONTAINS~Ann"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single().ShouldBe(new EntitiesDashboardCondition("Name", "contains", "Ann"));
    }

    [Fact]
    public void ParseConditions_ValueContainsTilde_KeepsTheWholeValue()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Name~eq~a~b~c"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single().Value.ShouldBe("a~b~c");
    }

    [Fact]
    public void ParseConditions_UnknownField_ReturnsValidationFailureInsteadOfThrowing()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Missing~eq~x"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>();
        result.Messages.Single().ShouldContain("Missing");
    }

    [Theory]
    [InlineData("ApiToken~eq~x")]
    [InlineData("InternalNote~eq~x")]
    public void ParseConditions_MaskedOrHiddenField_IsRejected(string filter)
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: [filter]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Name~gt~x")]
    [InlineData("IsVip~contains~x")]
    [InlineData("Age~startswith~1")]
    [InlineData("Name~bogus~x")]
    public void ParseConditions_OperatorNotAllowedForFieldKind_IsRejected(string filter)
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: [filter]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.Single().ShouldContain("operator");
    }

    [Fact]
    public void ParseConditions_MalformedCondition_IsRejected()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Name"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ParseConditions_MoreThanTwentyConditions_IsRejected()
    {
        // Arrange
        var filters = Enumerable.Range(0, EntitiesDashboardQuery.MaxConditions + 1).Select(i => $"Name~eq~{i}").ToList();
        var query = EntitiesDashboardTestData.Query(this.customer, filters: filters);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.Single().ShouldContain("20");
    }

    [Fact]
    public void ParseConditions_ExactlyTwentyConditions_IsAccepted()
    {
        // Arrange
        var filters = Enumerable.Range(0, EntitiesDashboardQuery.MaxConditions).Select(i => $"Name~eq~{i}").ToList();
        var query = EntitiesDashboardTestData.Query(this.customer, filters: filters);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(20);
    }

    [Fact]
    public void ParseConditions_ValueObjectMember_UsesTheMemberPath()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["address.city~eq~Springfield"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(this.customer, query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single().Path.ShouldBe("Address.City");
    }

    [Fact]
    public void ParseConditions_MemberOfMaskedValueObject_IsRejected()
    {
        // Arrange
        var entity = EntitiesDashboardTestData.DescribeCustomer(o => o.Entity<Customer>(e => e.Mask(c => c.Address)));
        var query = EntitiesDashboardTestData.Query(entity, filters: ["Address.City~eq~x"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.ParseConditions(entity, query);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void GetOperatorTokens_TextProperty_IncludesStringOperatorsButNoOrdering()
    {
        // Arrange
        var property = this.customer.FindProperty("Name");

        // Act
        var tokens = EntitiesDashboardFilterBuilder.GetOperatorTokens(property);

        // Assert
        tokens.ShouldContain("contains");
        tokens.ShouldContain("isempty");
        tokens.ShouldNotContain("gt");
    }

    [Fact]
    public void GetOperatorTokens_NumberProperty_IncludesOrderingButNoStringOperators()
    {
        // Arrange
        var property = this.customer.FindProperty("Age");

        // Act
        var tokens = EntitiesDashboardFilterBuilder.GetOperatorTokens(property);

        // Assert
        tokens.ShouldContain("gte");
        tokens.ShouldContain("lt");
        tokens.ShouldNotContain("contains");
    }

    [Fact]
    public void GetOperatorTokens_BooleanAndEnumProperties_AllowEqualityOnly()
    {
        // Arrange & Act
        var boolean = EntitiesDashboardFilterBuilder.GetOperatorTokens(this.customer.FindProperty("IsVip"));
        var enumeration = EntitiesDashboardFilterBuilder.GetOperatorTokens(this.customer.FindProperty("Status"));

        // Assert
        boolean.OrderBy(t => t).ShouldBe(["eq", "isnotnull", "isnull", "neq"]);
        enumeration.OrderBy(t => t).ShouldBe(["eq", "isnotnull", "isnull", "neq"]);
    }

    [Fact]
    public void Build_MatchAnd_CarriesAndLogicOnEveryCondition()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Name~eq~Ann", "Age~gt~18"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Filters.Select(f => f.Logic).ShouldAllBe(l => l == FilterLogicOperator.And);
        result.Value.Filters.Select(f => f.Field).ShouldBe(["Name", "Age"]);
    }

    [Fact]
    public void Build_MatchOr_ChainsConditionsWithOrExceptTheLast()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Name~eq~Ann", "Name~eq~Bob", "Age~gt~18"], match: "or");

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.Value.Filters.Select(f => f.Logic).ShouldBe([FilterLogicOperator.Or, FilterLogicOperator.Or, FilterLogicOperator.And]);
    }

    [Fact]
    public void Build_QuickSearch_ContainsGroupOverVisibleTopLevelTextFields()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, quickSearch: "spring");

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        var filters = result.Value.Filters;
        filters.ShouldAllBe(f => f.Operator == FilterOperator.Contains && (string)f.Value == "spring");
        var fields = filters.Select(f => f.Field).ToList();
        fields.ShouldContain("Name");
        fields.ShouldContain("Email");
        fields.ShouldNotContain("Address.City");
        fields.ShouldNotContain("ApiToken");
        fields.ShouldNotContain("InternalNote");
        filters.Take(filters.Count - 1).ShouldAllBe(f => f.Logic == FilterLogicOperator.Or);
        filters.Last().Logic.ShouldBe(FilterLogicOperator.And);
    }

    [Fact]
    public void Build_QuickSearchWithConditionsInOrMode_DoesNotChainTheSearchGroupWithOr()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Age~gt~1", "Age~lt~99"], quickSearch: "x", match: "or");

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        var filters = result.Value.Filters;
        var userStart = filters.Count - 2;
        filters[userStart - 1].Logic.ShouldBe(FilterLogicOperator.And);
        filters[userStart].Logic.ShouldBe(FilterLogicOperator.Or);
        filters[userStart + 1].Logic.ShouldBe(FilterLogicOperator.And);
    }

    [Fact]
    public void Build_TypedValues_AreParsedToTheClrType()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(
            this.customer,
            filters: ["Age~gte~21", "IsVip~eq~true", "CreditLimit~lt~12.5", "Status~eq~Suspended", "BirthDate~gt~2000-01-02T00:00:00Z"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Messages));
        var values = result.Value.Filters.Select(f => f.Value).ToList();
        values[0].ShouldBe(21);
        values[1].ShouldBe(true);
        values[2].ShouldBe(12.5m);
        values[3].ShouldBe(CustomerStatus.Suspended);
        values[4].ShouldBeOfType<DateTime>();
    }

    [Fact]
    public void Build_InvalidTypedValue_ReturnsFailureInsteadOfThrowing()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Age~eq~abc"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Build_NullOperators_HaveNoValue()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["BirthDate~isnull~", "Name~isnotempty~"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Filters.ShouldAllBe(f => f.Value == null);
    }

    [Fact]
    public void Build_GuidIdFilter_ParsesTheGuid()
    {
        // Arrange
        var entity = EntitiesDashboardTestData.Describe<Order>();
        var id = Guid.NewGuid();
        var query = EntitiesDashboardTestData.Query(entity, filters: [$"Id~eq~{id}"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(entity, query, 25);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Messages));
        result.Value.Filters.Single().Value.ShouldBe(id);
    }

    [Fact]
    public void Build_NoSortOnAuditableEntity_OrdersByUpdatedThenCreatedDescendingThenId()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.Value.Orderings.Select(o => (o.Field, o.Direction)).ShouldBe(
        [
            ("AuditState.UpdatedDate", OrderDirection.Descending),
            ("AuditState.CreatedDate", OrderDirection.Descending),
            ("Id", OrderDirection.Ascending)
        ]);
    }

    [Fact]
    public void Build_NoSortOnNonAuditableEntity_OrdersByIdOnly()
    {
        // Arrange
        var entity = EntitiesDashboardTestData.Describe<Region>();
        var query = EntitiesDashboardTestData.Query(entity);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(entity, query, 25);

        // Assert
        result.Value.Orderings.Select(o => (o.Field, o.Direction)).ShouldBe([("Id", OrderDirection.Ascending)]);
    }

    [Fact]
    public void Build_ExplicitSortDescending_PutsTheSortFieldFirstAndKeepsIdAsTieBreaker()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, sort: "Name", direction: "desc");

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.Value.Orderings.Select(o => (o.Field, o.Direction)).ShouldBe(
            [("Name", OrderDirection.Descending), ("Id", OrderDirection.Ascending)]);
    }

    [Theory]
    [InlineData("ApiToken")]
    [InlineData("InternalNote")]
    [InlineData("Address")]
    [InlineData("DoesNotExist")]
    public void Build_UnsortableMaskedOrHiddenSort_FallsBackToTheDefaultOrder(string sort)
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, sort: sort);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.Value.Orderings.First().Field.ShouldBe("AuditState.UpdatedDate");
    }

    [Fact]
    public void Build_SortByIdProperty_DoesNotAppendIdTwice()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, sort: "Id", direction: "desc");

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.Value.Orderings.Count(o => o.Field == "Id").ShouldBe(1);
        result.Value.Orderings.Single().Direction.ShouldBe(OrderDirection.Descending);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void Build_Batch_MapsToPageWithBatchSizeAsPageSize(int batch, int expectedPage)
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, batch: batch);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 40);

        // Assert
        result.Value.Page.ShouldBe(expectedPage);
        result.Value.PageSize.ShouldBe(40);
        result.Value.NoTracking.ShouldBeTrue();
    }

    [Fact]
    public void Build_InvalidCondition_PropagatesTheValidationError()
    {
        // Arrange
        var query = EntitiesDashboardTestData.Query(this.customer, filters: ["Nope~eq~x"]);

        // Act
        var result = EntitiesDashboardFilterBuilder.Build(this.customer, query, 25);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public void FindSortable_ScalarProperty_ReturnsProperty()
    {
        // Arrange & Act
        var property = EntitiesDashboardFilterBuilder.FindSortable(this.customer, "name");

        // Assert
        property.ShouldNotBeNull();
        property.Name.ShouldBe("Name");
    }

    [Fact]
    public void From_QueryCollection_ParsesFiltersBatchAndColumns()
    {
        // Arrange
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?entity=E&f=Name~eq~a&f=Age~gt~1&batch=-2&columns=Name, Email&match=OR&dir=DESC&q=%20x%20");

        // Act
        var query = EntitiesDashboardQuery.From(http.Request.Query);

        // Assert
        query.Entity.ShouldBe("E");
        query.Filters.ShouldBe(["Name~eq~a", "Age~gt~1"]);
        query.Batch.ShouldBe(1);
        query.Columns.ShouldBe(["Name", "Email"]);
        query.MatchAny.ShouldBeTrue();
        query.Descending.ShouldBeTrue();
        query.QuickSearch.ShouldBe("x");
    }
}
