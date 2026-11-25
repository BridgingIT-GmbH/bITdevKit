namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>Verifies the document Dimension encoding and the annotated EF model.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DocumentDimensionsModelTests</code></example>
public class DocumentDimensionsModelTests
{
    /// <summary>Verifies that the document primary and Dimension foreign keys share a relational mapping.</summary>
    /// <example><code>dotnet test --filter FullyQualifiedName~AnnotatedRowKeys_UseSharedGuidTextMapping</code></example>
    [Fact]
    public void Model_AnnotatedRowKeys_UseSharedGuidTextMapping()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new DocumentContext(options);
        var suppliedId = Guid.Parse("a24f807b-5591-49d0-8e72-f169e0db13f1");

        // Act
        var sut = context.GetService<IDesignTimeModel>().Model;
        var owner = sut.FindEntityType(typeof(StorageDocument));
        var dimension = sut.FindEntityType(typeof(StorageDocumentDimension));
        var ownerId = owner.FindProperty(nameof(StorageDocument.Id));
        var dimensionId = dimension.FindProperty(nameof(StorageDocumentDimension.DocumentId));

        // Assert
        new StorageDocument().Id.Version.ShouldBe(7);
        new StorageDocument { Id = suppliedId }.Id.ShouldBe(suppliedId);
        ownerId.ValueGenerated.ShouldBe(ValueGenerated.Never);
        owner.FindPrimaryKey().Properties.ShouldBe([ownerId]);
        dimension.FindPrimaryKey().Properties.Select(property => property.Name)
            .ShouldBe([nameof(StorageDocumentDimension.DocumentId), nameof(StorageDocumentDimension.NameId)]);
        var foreignKey = dimension.GetForeignKeys().Single(key => key.PrincipalEntityType == owner);
        foreignKey.Properties.ShouldBe([dimensionId]);
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);

        foreach (var key in new[] { ownerId, dimensionId })
        {
            key.ClrType.ShouldBe(typeof(Guid));
            key.GetColumnType().ShouldBe("char(32)");
            key.GetMaxLength().ShouldBe(32);
            key.IsUnicode().ShouldBe(false);
            key.IsFixedLength().ShouldBe(true);
            key.GetCollation().ShouldBeNull();
            var converter = key.GetValueConverter();
            converter.ProviderClrType.ShouldBe(typeof(string));
            var text = converter.ConvertToProvider(suppliedId);
            text.ShouldBe("a24f807b559149d08e72f169e0db13f1");
            converter.ConvertFromProvider(text).ShouldBe(suppliedId);
        }
    }

    [Fact]
    public void Encode_ValidScalars_RoundTripsExactLogicalValues()
    {
        var sut = new StorageDocumentDimension();
        foreach (var scalar in new object[] { null, int.MinValue, int.MaxValue, false, true, 1.000000m,
            999999999999.999999m, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), "", "EU ", "é\0" })
        {
            var value = DocumentDimensionValue.Create(scalar).Value;
            DocumentDimensionValueEncoding.Encode(value, sut);
            DocumentDimensionValueEncoding.Decode(sut).Value.ShouldBe(value);
        }
    }

    [Fact]
    public void Decode_InvalidDiscriminatorOrSlots_ReturnsFailure()
    {
        var sut = new StorageDocumentDimension { ValueType = DocumentDimensionValueType.Boolean, IntegralValue = 2 };
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = 1;
        sut.StringValue = "41";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.IntegralValue = null;
        sut.ValueType = DocumentDimensionValueType.String;
        sut.StringValue = "ff";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.StringValue = "FF";
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
        sut.ValueType = DocumentDimensionValueType.Null;
        DocumentDimensionValueEncoding.Decode(sut).IsFailure.ShouldBeTrue();
    }

    private sealed class DocumentContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Discovers the document and its Dimension entities through annotations.</summary>
        /// <example><code>var count = context.Documents.Count();</code></example>
        public DbSet<StorageDocument> Documents => this.Set<StorageDocument>();
    }
}
