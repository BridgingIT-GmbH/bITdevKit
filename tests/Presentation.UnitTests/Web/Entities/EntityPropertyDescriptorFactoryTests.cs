// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

public sealed class SamplePriority : Enumeration
{
    public static readonly SamplePriority Low = new(1, "Low");

    public static readonly SamplePriority High = new(2, "High");

    private SamplePriority(int id, string value)
        : base(id, value)
    {
    }
}

public class EditorKindSample : Entity<Guid>
{
    public SamplePriority Priority { get; set; }

    public string TextValue { get; set; }

    public char CharValue { get; set; }

    public TimeSpan Duration { get; set; }

    public DateOnly Day { get; set; }

    public bool Flag { get; set; }

    public Guid Reference { get; set; }

    public DateTime Moment { get; set; }

    public DateTimeOffset Offset { get; set; }

    public CustomerStatus Status { get; set; }

    public int Count { get; set; }

    public decimal? Amount { get; set; }

    public CustomerTier Tier { get; set; }

    public Address Address { get; set; }

    public List<string> Tags { get; set; }

    public byte[] Blob { get; set; }

    public Region Parent { get; set; }

    public List<Order> Orders { get; set; }

    public object Anything { get; set; }

    public string PrivateSetter { get; private set; }

    public string ComputedOnly => "computed";
}

[UnitTest("Presentation")]
public class EntityPropertyDescriptorFactoryTests
{
    private readonly EntitiesDashboardEntity sample = EntitiesDashboardTestData.Describe<EditorKindSample>();

    [Theory]
    [InlineData("TextValue", EntitiesDashboardEditorKind.Text)]
    [InlineData("CharValue", EntitiesDashboardEditorKind.Text)]
    [InlineData("Duration", EntitiesDashboardEditorKind.Text)]
    [InlineData("Day", EntitiesDashboardEditorKind.Text)]
    [InlineData("Flag", EntitiesDashboardEditorKind.Boolean)]
    [InlineData("Reference", EntitiesDashboardEditorKind.Guid)]
    [InlineData("Moment", EntitiesDashboardEditorKind.DateTime)]
    [InlineData("Offset", EntitiesDashboardEditorKind.DateTime)]
    [InlineData("Status", EntitiesDashboardEditorKind.Enum)]
    [InlineData("Count", EntitiesDashboardEditorKind.Number)]
    [InlineData("Amount", EntitiesDashboardEditorKind.Number)]
    [InlineData("Tier", EntitiesDashboardEditorKind.Enumeration)]
    [InlineData("Priority", EntitiesDashboardEditorKind.Enumeration)]
    [InlineData("Address", EntitiesDashboardEditorKind.Json)]
    [InlineData("Tags", EntitiesDashboardEditorKind.Json)]
    [InlineData("Blob", EntitiesDashboardEditorKind.Unsupported)]
    [InlineData("Parent", EntitiesDashboardEditorKind.Unsupported)]
    [InlineData("Orders", EntitiesDashboardEditorKind.Unsupported)]
    [InlineData("Anything", EntitiesDashboardEditorKind.Unsupported)]
    public void Create_PropertyType_MapsToTheExpectedEditorKind(    string name, object expected)
    {
        // Arrange & Act
        var property = this.sample.FindProperty(name);

        // Assert
        property.ShouldNotBeNull();
            property.EditorKind.ShouldBe((EntitiesDashboardEditorKind)expected);
    }

    [Fact]
    public void GetOptions_EnumerationClass_ListsDeclaredMembersByIdAndValue()
    {
        // Arrange & Act
        var options = EntitiesDashboardValueConverter.GetOptions(typeof(SamplePriority));

        // Assert
        options.Select(o => (o.Value, o.Label)).ShouldBe([("1", "Low"), ("2", "High")]);
    }

    [Fact]
    public void Parse_EnumerationClass_ResolvesTheMemberByIdOrValue()
    {
        // Arrange & Act
        var byId = EntitiesDashboardValueConverter.Parse(typeof(SamplePriority), "2", "Priority");
        var byValue = EntitiesDashboardValueConverter.Parse(typeof(SamplePriority), "low", "Priority");

        // Assert
        byId.Value.ShouldBe(SamplePriority.High);
        byValue.Value.ShouldBe(SamplePriority.Low);
    }

    [Fact]
    public void Create_NullableValueType_IsNullableWithUnderlyingType()
    {
        // Arrange & Act
        var property = this.sample.FindProperty("Amount");

        // Assert
        property.IsNullable.ShouldBeTrue();
        property.UnderlyingType.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Create_UnsupportedProperty_IsReadOnlyWithReason()
    {
        // Arrange & Act
        var property = this.sample.FindProperty("Blob");

        // Assert
        property.IsReadOnly.ShouldBeTrue();
        property.ReadOnlyReason.ShouldBe("This property type is not supported for editing.");
        property.IsFilterable.ShouldBeFalse();
        property.IsSortable.ShouldBeFalse();
    }

    [Fact]
    public void Create_PropertyWithNonPublicSetter_IsReadOnlyWithReason()
    {
        // Arrange & Act
        var property = this.sample.FindProperty("PrivateSetter");

        // Assert
        property.IsReadOnly.ShouldBeTrue();
        property.ReadOnlyReason.ShouldBe("The property has no public setter.");
    }

    [Fact]
    public void Create_GetterOnlyProperty_IsReadOnlyWithReason()
    {
        // Arrange & Act
        var property = this.sample.FindProperty("ComputedOnly");

        // Assert
        property.IsReadOnly.ShouldBeTrue();
        property.ReadOnlyReason.ShouldBe("The property has no public setter.");
    }

    [Fact]
    public void Create_AllowNonPublicSetters_MakesPrivateSetterWritable()
    {
        // Arrange
        var entity = EntitiesDashboardTestData.Describe<EditorKindSample>(o => o.Entity<EditorKindSample>(e => e.AllowNonPublicSetters()));

        // Act
        var property = entity.FindProperty("PrivateSetter");

        // Assert
        property.IsReadOnly.ShouldBeFalse();
        property.Setter.ShouldNotBeNull();
    }

    [Fact]
    public void Create_IdProperty_IsReadOnlyAndFirstWithGuidPrimitive()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.DescribeCustomer();

        // Assert
        entity.Properties.First().IsId.ShouldBeTrue();
        entity.IdProperty.IsReadOnly.ShouldBeTrue();
        entity.IdProperty.ReadOnlyReason.ShouldBe("The Id cannot be changed after creation.");
        entity.IdProperty.IsTypedId.ShouldBeTrue();
        entity.IdProperty.EditorKind.ShouldBe(EntitiesDashboardEditorKind.Guid);
        entity.IdPrimitiveType.ShouldBe(typeof(Guid));
        entity.IdType.ShouldBe(typeof(CustomerId));
    }

    [Fact]
    public void Create_AuditableConcurrencyEntity_PlacesConcurrencyAndAuditLastAsReadOnly()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.DescribeCustomer();

        // Assert
        entity.IsAuditable.ShouldBeTrue();
        entity.IsConcurrency.ShouldBeTrue();
        entity.Properties.TakeLast(2).Select(p => p.Name).ShouldBe(["ConcurrencyVersion", "AuditState"]);
        var concurrency = entity.FindProperty("ConcurrencyVersion");
        concurrency.IsConcurrency.ShouldBeTrue();
        concurrency.IsReadOnly.ShouldBeTrue();
        concurrency.ReadOnlyReason.ShouldBe("The concurrency version is managed by the store.");
        var audit = entity.FindProperty("AuditState");
        audit.IsAudit.ShouldBeTrue();
        audit.IsReadOnly.ShouldBeTrue();
        audit.ReadOnlyReason.ShouldBe("Audit state is read-only.");
    }

    [Fact]
    public void Create_ValueObject_ExposesMembersAsReadOnlyFilterableButNotSortable()
    {
        // Arrange & Act
        var address = EntitiesDashboardTestData.DescribeCustomer().FindProperty("Address");

        // Assert
        address.Members.Select(m => m.Path).ShouldBe(["Address.Street", "Address.City", "Address.TaxNumber"], ignoreOrder: true);
        address.IsFilterable.ShouldBeFalse();
        var city = address.Members.Single(m => m.Name == "City");
        city.IsReadOnly.ShouldBeTrue();
        city.ReadOnlyReason.ShouldBe("Value-object members are edited through the JSON of their parent property.");
        city.IsFilterable.ShouldBeTrue();
        city.IsSortable.ShouldBeFalse();
    }

    [Fact]
    public void Create_MaskedAndHiddenProperties_AreNotFilterableOrSortable()
    {
        // Arrange
        var entity = EntitiesDashboardTestData.DescribeCustomer(o =>
            o.Entity<Customer>(e => e.Mask(c => c.ApiToken, "xx").Hide(c => c.InternalNote)));

        // Act
        var masked = entity.FindProperty("ApiToken");
        var hidden = entity.FindProperty("InternalNote");

        // Assert
        masked.IsMasked.ShouldBeTrue();
        masked.MaskText.ShouldBe("xx");
        masked.IsFilterable.ShouldBeFalse();
        masked.IsSortable.ShouldBeFalse();
        hidden.IsHidden.ShouldBeTrue();
        hidden.IsVisible.ShouldBeFalse();
        hidden.IsFilterable.ShouldBeFalse();
    }

    [Fact]
    public void Create_ScalarProperty_IsFilterableAndSortable()
    {
        // Arrange & Act
        var name = EntitiesDashboardTestData.DescribeCustomer().FindProperty("Name");

        // Assert
        name.IsFilterable.ShouldBeTrue();
        name.IsSortable.ShouldBeTrue();
        name.IsReadOnly.ShouldBeFalse();
        name.IsWritable.ShouldBeTrue();
    }

    [Fact]
    public void Create_EntityWithParameterlessConstructorAndPublicSetters_CanCreateUpdateAndDelete()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.DescribeCustomer();

        // Assert
        entity.CanCreate.ShouldBeTrue();
        entity.CanUpdate.ShouldBeTrue();
        entity.CanDelete.ShouldBeTrue();
        entity.CapabilityNote.ShouldBeNull();
        entity.Creator.ShouldNotBeNull();
    }

    [Fact]
    public void Create_EntityWithoutParameterlessConstructorAndPrivateSetters_CannotCreateOrUpdateAndExplainsWhy()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.Describe<LockedProduct>();

        // Assert
        entity.CanCreate.ShouldBeFalse();
        entity.CanUpdate.ShouldBeFalse();
        entity.CanDelete.ShouldBeTrue();
        entity.CapabilityNote.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_AllowNonPublicSettersOnLockedEntity_EnablesUpdate()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.Describe<LockedProduct>(o => o.Entity<LockedProduct>(e => e.AllowNonPublicSetters()));

        // Assert
        entity.CanUpdate.ShouldBeTrue();
        entity.CanCreate.ShouldBeFalse();
    }

    [Fact]
    public void Create_SoftDeletableEntity_IsFlagged()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.Describe<ArchivedNote>();

        // Assert
        entity.IsSoftDeletable.ShouldBeTrue();
        EntitiesDashboardTestData.DescribeCustomer().IsSoftDeletable.ShouldBeFalse();
    }

    [Fact]
    public void Create_Entity_UsesFullNameAsKeyAndShortNameAsName()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.Describe<Region>();

        // Assert
        entity.Key.ShouldBe(typeof(Region).FullName);
        entity.Name.ShouldBe("Region");
        entity.IdPrimitiveType.ShouldBe(typeof(int));
        entity.DefaultSource.ShouldBeOfType<RepositoryEntitiesDashboardSource>();
    }

    [Fact]
    public void Create_DomainEventsOfAggregateRoots_AreSkipped()
    {
        // Arrange & Act
        var entity = EntitiesDashboardTestData.Describe<EditorKindAggregate>();

        // Assert
        entity.Properties.ShouldNotContain(p => p.Name == "DomainEvents");
    }

    public class EditorKindAggregate : AggregateRoot<Guid>
    {
        public string Title { get; set; }
    }
}
