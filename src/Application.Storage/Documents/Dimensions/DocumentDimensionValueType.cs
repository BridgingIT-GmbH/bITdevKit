// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Identifies the logical kind of a document dimension.</summary>
/// <example><code>var kind = DocumentDimensionValue.Create(42).Value.ValueType; // Integer</code></example>
public enum DocumentDimensionValueType : byte
{
    /// <summary>An explicit null value.</summary>
    Null = 0,
    /// <summary>A signed Int32 value.</summary>
    Integer = 1,
    /// <summary>An exact decimal with precision 18 and scale 6.</summary>
    Decimal = 2,
    /// <summary>A Boolean value.</summary>
    Boolean = 3,
    /// <summary>An exact Unicode string.</summary>
    String = 4,
    /// <summary>A UTC timestamp.</summary>
    DateTime = 5
}
