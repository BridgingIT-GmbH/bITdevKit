// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Application.Storage;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>A typed, automatically indexed document dimension value.</summary>
/// <example><code>var page = await context.Set&lt;StorageDocumentDimension&gt;().AsNoTracking().Where(row =&gt; row.NameId == nameId).Take(100).ToListAsync();</code></example>
[Table("__Storage_DocumentDimensions")]
[PrimaryKey(nameof(DocumentId), nameof(NameId))]
[Index(nameof(NameId), nameof(ValueType), nameof(IntegralValue), nameof(DocumentId))]
[Index(nameof(NameId), nameof(ValueType), nameof(DecimalValue), nameof(DocumentId))]
[Index(nameof(NameId), nameof(ValueType), nameof(StringValue), nameof(DocumentId))]
public sealed class StorageDocumentDimension
{
    /// <summary>Gets or sets the owning record identity.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Gets or sets the scoped dictionary identity.</summary>
    public long NameId { get; set; }

    /// <summary>Gets or sets the logical discriminator.</summary>
    public DocumentDimensionValueType ValueType { get; set; }

    /// <summary>Gets or sets an Int32, Boolean 0/1, or UTC ticks.</summary>
    public long? IntegralValue { get; set; }

    /// <summary>Gets or sets an exact validated decimal.</summary>
    [Precision(18, 6)]
    public decimal? DecimalValue { get; set; }

    /// <summary>Gets or sets uppercase hexadecimal strict UTF-8 search text.</summary>
    [MaxLength(512), Unicode(false)]
    public string StringValue { get; set; }

    /// <summary>Gets or sets the required owner.</summary>
    [ForeignKey(nameof(DocumentId))]
    public StorageDocument Owner { get; set; }

    /// <summary>Gets or sets the required dictionary entry.</summary>
    [ForeignKey(nameof(NameId))]
    public StorageDocumentDimensionName Name { get; set; }
}
