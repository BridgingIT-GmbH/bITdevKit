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
    /// <summary>Deserializes a URL query string into filtering, ordering, inclusion, hierarchy, and paging criteria.</summary>
    /// <param name="queryString">The encoded filter-model query string, with or without a leading question mark.</param>
    /// <returns>The parsed filter model; an empty model is returned for blank input.</returns>
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
    /// <summary>Requires all combined criteria to match.</summary>
    [EnumMember(Value = "and")]
    And,

    /// <summary>Requires at least one combined criterion to match.</summary>
    [EnumMember(Value = "or")]
    Or
}

/// <summary>
/// Specifies the operators that can be used in filter criteria.
/// </summary>
public enum FilterOperator
{
    /// <summary>Matches values that are equal.</summary>
    [EnumMember(Value = "eq")]
    Equal,

    /// <summary>Matches values that are not equal.</summary>
    [EnumMember(Value = "neq")]
    NotEqual,

    /// <summary>Matches null values.</summary>
    [EnumMember(Value = "isnull")]
    IsNull,

    /// <summary>Matches non-null values.</summary>
    [EnumMember(Value = "isnotnull")]
    IsNotNull,

    /// <summary>Matches empty values.</summary>
    [EnumMember(Value = "isempty")]
    IsEmpty,

    /// <summary>Matches values that are not empty.</summary>
    [EnumMember(Value = "isnotempty")]
    IsNotEmpty,

    /// <summary>Matches values greater than the comparison value.</summary>
    [EnumMember(Value = "gt")]
    GreaterThan,

    /// <summary>Matches values greater than or equal to the comparison value.</summary>
    [EnumMember(Value = "gte")]
    GreaterThanOrEqual,

    /// <summary>Matches values less than the comparison value.</summary>
    [EnumMember(Value = "lt")]
    LessThan,

    /// <summary>Matches values less than or equal to the comparison value.</summary>
    [EnumMember(Value = "lte")]
    LessThanOrEqual,

    /// <summary>Matches text containing the comparison text.</summary>
    [EnumMember(Value = "contains")]
    Contains,

    /// <summary>Matches text that does not contain the comparison text.</summary>
    [EnumMember(Value = "doesnotcontain")] // string only
    DoesNotContain,

    /// <summary>Matches text beginning with the comparison text.</summary>
    [EnumMember(Value = "startswith")]
    StartsWith,

    /// <summary>Matches text that does not begin with the comparison text.</summary>
    [EnumMember(Value = "doesnotstartwith")] // string only
    DoesNotStartWith,

    /// <summary>Matches text ending with the comparison text.</summary>
    [EnumMember(Value = "endswith")]
    EndsWith,

    /// <summary>Matches text that does not end with the comparison text.</summary>
    [EnumMember(Value = "doesnotendwith")] // string only
    DoesNotEndWith,

    /// <summary>Matches when any child criterion is satisfied.</summary>
    [EnumMember(Value = "any")]
    Any, // children

    /// <summary>Matches when all child criteria are satisfied.</summary>
    [EnumMember(Value = "all")]
    All, // children

    /// <summary>Matches when no child criterion is satisfied.</summary>
    [EnumMember(Value = "none")]
    None, // children
}

/// <summary>
/// Specifies custom filter types that allow for specialized filtering logic.
/// </summary>
public enum FilterCustomType
{
    /// <summary>Indicates that no custom filtering behavior is requested.</summary>
    [EnumMember(Value = "none")]
    None,

    /// <summary>Performs a full-text search using a search term and selected fields.</summary>
    [EnumMember(Value = "fulltextsearch")] // params: searchTerm, fields
    FullTextSearch,

    /// <summary>Filters a field between start and end dates.</summary>
    [EnumMember(Value = "daterange")] // params: field, startDate, endDate, inclusive
    DateRange,

    /// <summary>Filters a date field relative to a reference date and direction.</summary>
    [EnumMember(Value = "daterelative")] // params: field, unit (day/week/month/year), amount, direction (past/future)
    DateRelative,

    /// <summary>Filters a field between start and end times.</summary>
    [EnumMember(Value = "timerange")] // params: field, startTime, endTime, inclusive
    TimeRange,

    /// <summary>Filters a time field relative to a reference time and direction.</summary>
    [EnumMember(Value = "timerelative")] // params: field, unit (minute/hour), amount, direction (past/future)
    TimeRelative,

    /// <summary>Filters a numeric field between minimum and maximum values.</summary>
    [EnumMember(Value = "numericrange")] // params: field, min, max
    NumericRange,

    /// <summary>Filters for a null field value.</summary>
    [EnumMember(Value = "isnull")] // params: field
    IsNull,

    /// <summary>Filters for a non-null field value.</summary>
    [EnumMember(Value = "isnotnull")] // params:field
    IsNotNull,

    /// <summary>Filters an enum field to a supplied set of values.</summary>
    [EnumMember(Value = "enumvalues")] // params: field, values
    EnumValues,

    /// <summary>Filters text to values contained in a supplied set.</summary>
    [EnumMember(Value = "textin")] // params: field, values
    TextIn,

    /// <summary>Filters text to values absent from a supplied set.</summary>
    [EnumMember(Value = "textnotin")] // params: field, values
    TextNotIn,

    /// <summary>Filters numeric values to those contained in a supplied set.</summary>
    [EnumMember(Value = "numericin")] // params: field, values
    NumericIn,

    /// <summary>Filters numeric values to those absent from a supplied set.</summary>
    [EnumMember(Value = "numericnotin")] // params: field, values
    NumericNotIn,

    /// <summary>Invokes a registered specification by name and arguments.</summary>
    [EnumMember(Value = "namedspecification")]
    NamedSpecification,

    /// <summary>Evaluates a tree of named specification nodes.</summary>
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
    /// <summary>Orders values from lowest to highest.</summary>
    [EnumMember(Value = "asc")]
    Ascending,

    /// <summary>Orders values from highest to lowest.</summary>
    [EnumMember(Value = "desc")]
    Descending
}

/// <summary>Defines standard result-page capacities for filter-model queries.</summary>
public enum PageSize
{
    /// <summary>Uses an extra-small page containing 5 items.</summary>
    [EnumMember(Value = "xs")]
    ExtraSmall = 5,      // extra small page size

    /// <summary>Uses a small page containing 10 items.</summary>
    [EnumMember(Value = "s")]
    Small = 10,      // small page size

    /// <summary>Uses a medium page containing 25 items.</summary>
    [EnumMember(Value = "m")]
    Medium = 25,     // medium page size

    /// <summary>Uses a large page containing 50 items.</summary>
    [EnumMember(Value = "l")]
    Large = 50,      // large page size

    /// <summary>Uses an extra-large page containing 100 items.</summary>
    [EnumMember(Value = "xl")]
    ExtraLarge = 100, // Extra large page size

    /// <summary>Uses an extra-extra-large page containing 1,000 items.</summary>
    [EnumMember(Value = "xxl")]
    ExtraExtraLarge = 1000 // Extra extra large page size
}
