// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.Serialization;

/// <summary>
/// Represents a model for building filter criteria for data queries.
/// Contains properties for pagination, ordering, filtering, and including related entities.
/// </summary>
public partial class FilterModel
{
    public static FilterModel FromQueryString(string queryString)
    {
        return FilterModelExtensions.FromQueryString(queryString);
    }

    /// <summary>
    /// Gets or sets the page number for pagination.
    /// Default value is 1.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// Default value is 10.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to disable change tracking for the query.
    /// Default value is <c>true</c>.
    /// </summary>
    public bool NoTracking { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of ordering criteria.
    /// </summary>
    public List<FilterOrderCriteria> Orderings { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of filter criteria.
    /// </summary>
    public List<FilterCriteria> Filters { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of related entities to include in the query.
    /// </summary>
    public List<string> Includes { get; set; } = [];

    /// <summary>
    /// Gets or sets the children to include in the query.
    /// </summary>
    public string Hierarchy { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth for including child entities.
    /// </summary>
    public int HierarchyMaxDepth { get; set; } = 5;

    /// <summary>
    /// Returns a string that represents the current model.
    /// </summary>
    /// <returns>
    /// A string that represents the current <see cref="FilterModel"/> instance, including page, page size,
    /// orderings, filters, and includes details.
    /// </returns>
    public override string ToString()
    {
        var orderingsString = this.Orderings.Count > 0
            ? string.Join(", ", this.Orderings.Select(o => $"{o.Field} {o.Direction}"))
            : "None";

        var filtersString = this.Filters.Count > 0
            ? string.Join("; ", this.Filters.Select(f => $"{f.Field} {f.Operator} {f.Value}"))
            : "None";

        var includesString = this.Includes.Count > 0
            ? string.Join(", ", this.Includes)
            : "None";

        return $"Page: {this.Page}, PageSize: {this.PageSize}, Orderings: {orderingsString}, Filters: {filtersString}, Includes: {includesString}";
    }
}

/// <summary>
/// Represents a single filter criterion, including the field to filter on, the operator, and the value.
/// </summary>
public class FilterCriteria
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilterCriteria"/> class.
    /// </summary>
    public FilterCriteria() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterCriteria"/> class with specified field, operator, and value.
    /// </summary>
    /// <param name="field">The field to filter on.</param>
    /// <param name="operator">The operator to use for filtering.</param>
    /// <param name="value">The value to compare against.</param>
    public FilterCriteria(string field, FilterOperator @operator, object value)
    {
        this.Field = field;
        this.Operator = @operator;
        this.Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterCriteria"/> class for custom filters.
    /// </summary>
    /// <param name="customType">The type of the custom filter.</param>
    /// <param name="customParameters">Optional custom parameters for the filter.</param>
    public FilterCriteria(FilterCustomType customType, Dictionary<string, object> customParameters = null)
    {
        this.CustomType = customType;
        this.CustomParameters = customParameters;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterCriteria"/> class for named specifications.
    /// </summary>
    /// <param name="specificationName">The name of the specification.</param>
    /// <param name="specificationArguments">The arguments for the specification.</param>
    public FilterCriteria(string specificationName, object[] specificationArguments)
    {
        this.SpecificationName = specificationName;
        this.SpecificationArguments = specificationArguments;
    }

    /// <summary>
    /// Gets or sets the field to filter on.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the operator to use for filtering.
    /// </summary>
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// Gets or sets the logic operator for combining multiple filters.
    /// Default value is <see cref="FilterLogicOperator.And"/>.
    /// </summary>
    public FilterLogicOperator Logic { get; set; } = FilterLogicOperator.And; // TODO: make optional

    /// <summary>
    /// Gets or sets the nested filters to apply.
    /// </summary>
    public List<FilterCriteria> Filters { get; set; } = []; // self referencing

    /// <summary>
    /// Gets or sets the custom filter type.
    /// Default value is <see cref="FilterCustomType.None"/>.
    /// </summary>
    public FilterCustomType CustomType { get; set; } = FilterCustomType.None; // TODO: make optional

    /// <summary>
    /// Gets or sets the custom parameters for the filter.
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; set; }

    /// <summary>
    /// Gets or sets the name of a named specification.
    /// </summary>
    public string SpecificationName { get; set; }

    /// <summary>
    /// Gets or sets the arguments for a named specification.
    /// </summary>
    public object[] SpecificationArguments { get; set; }

    /// <summary>
    /// Gets or sets a composite specification that can combine multiple filter criteria.
    /// </summary>
    public CompositeSpecification CompositeSpecification { get; set; }
}

/// <summary>
/// Represents an ordering criterion for sorting the results.
/// </summary>
public class FilterOrderCriteria
{
    /// <summary>
    /// Gets or sets the field to order by.
    /// </summary>
    public string Field { get; set; } // TODO: rename to Name

    /// <summary>
    /// Gets or sets the direction of the ordering.
    /// Default value is <see cref="OrderDirection.Ascending"/>.
    /// </summary>
    public OrderDirection Direction { get; set; } = OrderDirection.Ascending;
}

/// <summary>
/// Specifies the logical operators for combining filter criteria.
/// </summary>
public enum FilterLogicOperator
{
    [EnumMember(Value = "and")]
    And,

    [EnumMember(Value = "or")]
    Or
}

/// <summary>
/// Specifies the operators that can be used in filter criteria.
/// </summary>
public enum FilterOperator
{
    [EnumMember(Value = "eq")]
    Equal,

    [EnumMember(Value = "neq")]
    NotEqual,

    [EnumMember(Value = "isnull")]
    IsNull,

    [EnumMember(Value = "isnotnull")]
    IsNotNull,

    [EnumMember(Value = "isempty")]
    IsEmpty,

    [EnumMember(Value = "isnotempty")]
    IsNotEmpty,

    [EnumMember(Value = "gt")]
    GreaterThan,

    [EnumMember(Value = "gte")]
    GreaterThanOrEqual,

    [EnumMember(Value = "lt")]
    LessThan,

    [EnumMember(Value = "lte")]
    LessThanOrEqual,

    [EnumMember(Value = "contains")]
    Contains,

    [EnumMember(Value = "doesnotcontain")] // string only
    DoesNotContain,

    [EnumMember(Value = "startswith")]
    StartsWith,

    [EnumMember(Value = "doesnotstartwith")] // string only
    DoesNotStartWith,

    [EnumMember(Value = "endswith")]
    EndsWith,

    [EnumMember(Value = "doesnotendwith")] // string only
    DoesNotEndWith,

    [EnumMember(Value = "any")]
    Any, // children

    [EnumMember(Value = "all")]
    All, // children

    [EnumMember(Value = "none")]
    None, // children
}

/// <summary>
/// Specifies custom filter types that allow for specialized filtering logic.
/// </summary>
public enum FilterCustomType
{
    [EnumMember(Value = "none")]
    None,

    [EnumMember(Value = "fulltextsearch")] // params: searchTerm, fields
    FullTextSearch,

    [EnumMember(Value = "daterange")] // params: field, startDate, endDate, inclusive
    DateRange,

    [EnumMember(Value = "daterelative")] // params: field, unit (day/week/month/year), amount, direction (past/future)
    DateRelative,

    [EnumMember(Value = "timerange")] // params: field, startTime, endTime, inclusive
    TimeRange,

    [EnumMember(Value = "timerelative")] // params: field, unit (minute/hour), amount, direction (past/future)
    TimeRelative,

    [EnumMember(Value = "numericrange")] // params: field, min, max
    NumericRange,

    [EnumMember(Value = "isnull")] // params: field
    IsNull,

    [EnumMember(Value = "isnotnull")] // params:field
    IsNotNull,

    [EnumMember(Value = "enumvalues")] // params: field, values
    EnumValues,

    [EnumMember(Value = "textin")] // params: field, values
    TextIn,

    [EnumMember(Value = "textnotin")] // params: field, values
    TextNotIn,

    [EnumMember(Value = "numericin")] // params: field, values
    NumericIn,

    [EnumMember(Value = "numericnotin")] // params: field, values
    NumericNotIn,

    [EnumMember(Value = "namedspecification")]
    NamedSpecification,

    [EnumMember(Value = "compositespecification")]
    CompositeSpecification
}

/// <summary>
/// Represents a composite specification that can contain multiple specification nodes.
/// </summary>
public class CompositeSpecification
{
    /// <summary>
    /// Gets or sets the list of nodes in the composite specification.
    /// </summary>
    public List<SpecificationNode> Nodes { get; set; } = [];
}

/// <summary>
/// Represents a base class for specification nodes.
/// </summary>
public abstract class SpecificationNode;

/// <summary>
/// Represents a leaf node in a specification tree.
/// </summary>
public class SpecificationLeaf : SpecificationNode
{
    /// <summary>
    /// Gets or sets the name of the registered specification.
    /// </summary>
    public string Name { get; set; } // name of registered specification

    /// <summary>
    /// Gets or sets the arguments for the specification.
    /// </summary>
    public object[] Arguments { get; set; }
}

/// <summary>
/// Represents a group of specification nodes, combined by a logical operator.
/// </summary>
public class SpecificationGroup : SpecificationNode
{
    /// <summary>
    /// Gets or sets the logical operator used to combine the nodes.
    /// </summary>
    public FilterLogicOperator Logic { get; set; }

    /// <summary>
    /// Gets or sets the list of nodes in the specification group.
    /// </summary>
    public List<SpecificationNode> Nodes { get; set; } = []; // recursion
}

/// <summary>
/// Specifies the order direction for ordering results.
/// </summary>
public enum OrderDirection
{
    [EnumMember(Value = "asc")]
    Ascending,

    [EnumMember(Value = "desc")]
    Descending
}

public enum PageSize
{
    [EnumMember(Value = "xs")]
    ExtraSmall = 5,      // extra small page size

    [EnumMember(Value = "s")]
    Small = 10,      // small page size

    [EnumMember(Value = "m")]
    Medium = 25,     // medium page size

    [EnumMember(Value = "l")]
    Large = 50,      // large page size

    [EnumMember(Value = "xl")]
    ExtraLarge = 100, // Extra large page size

    [EnumMember(Value = "xxl")]
    ExtraExtraLarge = 1000 // Extra extra large page size
}