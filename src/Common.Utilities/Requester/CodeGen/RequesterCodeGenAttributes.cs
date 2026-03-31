// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks a partial request type as a source-generated command.
/// </summary>
/// <remarks>
/// When no explicit response type is provided, the command response is inferred from the
/// <see cref="HandleAttribute"/> method return type. The source generator emits the Requester
/// plumbing, validator glue, and convenience helpers.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAttribute"/> class and infers the response type from the handle method.
    /// </summary>
    public CommandAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAttribute"/> class for a command returning a value.
    /// </summary>
    /// <param name="responseType">The response type returned by the generated Requester handler.</param>
    public CommandAttribute(Type responseType)
    {
        this.ResponseType = responseType;
    }

    /// <summary>
    /// Gets the explicit response type when the command response should not be inferred from the handle method.
    /// </summary>
    public Type ResponseType { get; }
}

/// <summary>
/// Marks a partial request type as a source-generated query.
/// </summary>
/// <remarks>
/// When no explicit response type is provided, the query response is inferred from the
/// <see cref="HandleAttribute"/> method return type. Queries must still resolve to a non-<see cref="Unit"/> response type.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class QueryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAttribute"/> class and infers the response type from the handle method.
    /// </summary>
    public QueryAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAttribute"/> class with an explicit response type.
    /// </summary>
    /// <param name="responseType">The response type returned by the generated Requester handler.</param>
    public QueryAttribute(Type responseType)
    {
        this.ResponseType = responseType;
    }

    /// <summary>
    /// Gets the explicit response type when the query response should not be inferred from the handle method.
    /// </summary>
    public Type ResponseType { get; }
}

/// <summary>
/// Marks the developer-authored business-logic method that the Requester source generator should invoke.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class HandleAttribute : Attribute;

/// <summary>
/// Marks the developer-authored validation method that source generators should expose through FluentValidation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ValidateAttribute : Attribute;
