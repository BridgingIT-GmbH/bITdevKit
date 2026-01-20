// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Factory class for creating strongly-typed result errors organized by category.
/// </summary>
/// <remarks>
/// This static factory provides a fluent, discoverable API for creating errors across different domains.
/// Errors are organized into logical categories to help developers quickly find the right error type.
/// </remarks>
/// <example>
/// <code>
/// // Domain errors
/// var error = Errors.Domain.EntityNotFound();
///
/// // Validation errors
/// var error = Errors.Validation.InvalidFormat("JSON parse failed");
///
/// // Security errors
/// var error = Errors.Security.Unauthorized();
///
/// // External service errors
/// var error = Errors.External.Http(statusCode: 404, url: "https://api.example.com/users/123");
/// </code>
/// </example>
public static partial class Errors
{
    /// <summary>Domain and business logic errors.</summary>
    public static partial class Domain { }

    /// <summary>Input validation and data format errors.</summary>
    public static partial class Validation { }

    /// <summary>Data persistence and consistency errors.</summary>
    public static partial class Data { }

    /// <summary>Authentication and authorization errors.</summary>
    public static partial class Security { }

    /// <summary>External service and API errors.</summary>
    public static partial class External { }

    /// <summary>File system and blob storage errors.</summary>
    public static partial class Storage { }

    /// <summary>Resource availability and quota errors.</summary>
    public static partial class Resource { }

    /// <summary>Operation lifecycle and control errors.</summary>
    public static partial class Operation { }

    /// <summary>Technical and infrastructure errors.</summary>
    public static partial class Technical { }

    /// <summary>General-purpose errors.</summary>
    public static partial class General { }
}