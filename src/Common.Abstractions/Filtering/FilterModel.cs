// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.Serialization;

public class FilterModel
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public List<FilterOrderCriteria> Orderings { get; set; } = [];

    public List<FilterCriteria> Filters { get; set; } = [];

    public List<string> Includes { get; set; } = [];
}

public class FilterCriteria
{
    public FilterCriteria() { }

    public FilterCriteria(string field, FilterOperator @operator, object value)
    {
        this.Field = field;
        this.Operator = @operator;
        this.Value = value;
    }

    public FilterCriteria(FilterCustomType customType, Dictionary<string, object> customParameters = null)
    {
        this.CustomType = customType;
        this.CustomParameters = customParameters;
    }

    public FilterCriteria(string specificationName, object[] specificationArguments)
    {
        this.SpecificationName = specificationName;
        this.SpecificationArguments = specificationArguments;
    }

    public string Field { get; set; }

    public FilterOperator Operator { get; set; }

    public object Value { get; set; }

    public FilterLogicOperator Logic { get; set; } = FilterLogicOperator.And; // TODO: make optional

    public List<FilterCriteria> Filters { get; set; } = [];

    public FilterCustomType CustomType { get; set; } = FilterCustomType.None; // TODO: make optional

    public Dictionary<string, object> CustomParameters { get; set; }

    public string SpecificationName { get; set; }

    public object[] SpecificationArguments { get; set; }

    public CompositeSpecification CompositeSpecification { get; set; }
}

public class FilterOrderCriteria
{
    public string Field { get; set; } // TODO: rename to Name

    public OrderDirection Direction { get; set; } = OrderDirection.Ascending;
}

public enum FilterLogicOperator
{
    [EnumMember(Value = "and")]
    And,

    [EnumMember(Value = "or")]
    Or
}

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

public class CompositeSpecification
{
    public List<SpecificationNode> Nodes { get; set; } = [];
}


public abstract class SpecificationNode { }

public class SpecificationLeaf : SpecificationNode
{
    public string Name { get; set; } // name of registered specification

    public object[] Arguments { get; set; }
}

public class SpecificationGroup : SpecificationNode
{
    public FilterLogicOperator Logic { get; set; }

    public List<SpecificationNode> Nodes { get; set; } = [];
}

public enum OrderDirection
{
    [EnumMember(Value = "asc")]
    Ascending,

    [EnumMember(Value = "desc")]
    Descending
}