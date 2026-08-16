// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a model for building filter criteria for data queries.
/// Contains properties for pagination, ordering, filtering, and including related entities.
/// </summary>
public partial class FilterModel
{
    /// <summary>
    /// Internal property to ensure FilterCriteria schema is generated (OpenApi generator).
    /// This is not used by the API but forces OpenAPI schema generation.
    /// </summary>
    public FilterCriteria _FilterCriteriaSchema => null;

    /// <summary>
    /// Internal property to ensure CompositeSpecification schema is generated (OpenApi generator).
    /// </summary>
    public CompositeSpecification _CompositeSpecificationSchema => null;

    /// <summary>
    /// Internal property to ensure SpecificationNode schema is generated (OpenApi generator).
    /// </summary>
    public SpecificationNode _SpecificationNodeSchema => null;

    //public FilterOperator _FilterOperatorSchema; // TODO: not in openapi schema

    //public FilterLogicOperator _FilterLogicOperatorSchema; // TODO: not in openapi schema

    //public FilterCustomType _FilterCustomTypeSchema; // TODO: not in openapi schema

    /// <summary>
    /// Exposes the <see cref="SpecificationLeaf"/> type so OpenAPI generators include its schema.
    /// </summary>
    public SpecificationLeaf _SpecificationLeafSchema;

    /// <summary>
    /// Exposes the <see cref="SpecificationGroup"/> type so OpenAPI generators include its schema.
    /// </summary>
    public SpecificationGroup _SpecificationGroupSchema;
}
