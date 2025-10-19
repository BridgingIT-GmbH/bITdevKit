// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

/// <summary>
/// Represents a ProblemDetails response containing a serialized Result object
/// and optional validation or diagnostic information in the <c>data</c> property.
/// </summary>
public class ResultProblemDetails : ProblemDetails
{
    /// <summary>
    /// Additional contextual data attached to this problem.
    /// </summary>
    public ResultProblemData Data { get; set; } = new ResultProblemData();

    ///// <summary>
    ///// Holds arbitrary extension members that are not explicitly declared.
    ///// </summary>
    //[JsonExtensionData]
    //public IDictionary<string, object> ExtensionData1 { get; set; } =
    //    new Dictionary<string, object>();
}

/// <summary>
/// Data section included inside a <see cref="ResultProblemDetails"/> response.
/// </summary>
public class ResultProblemData
{
    /// <summary>
    /// The serialized Result information containing messages and errors.
    /// </summary>
    public ResultProblemResult Result { get; set; } = new ResultProblemResult();

    /// <summary>
    /// Additional other (non result) error information 
    /// </summary>
    //public string Errors { get; set; } = string.Empty;
    public IEnumerable<ProblemError> Errors { get; set; } = [];

    /// <summary>
    /// Arbitrary extension properties for this data object.
    /// </summary>
    //public IDictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// The serialized Result description, representing the operation outcome.
/// </summary>
public class ResultProblemResult
{
    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// List of messages associated with the Result.
    /// </summary>
    public IEnumerable<string> Messages { get; set; } = [];

    /// <summary>
    /// Array of domain or technical errors, each with at least a 'message' property
    /// and optional additional data fields.
    /// </summary>
    public IEnumerable<ResultProblemError> Errors { get; set; } = [];

    //public IDictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents an individual error entry included in a Result.
/// </summary>
public class ResultProblemError
{
    /// <summary>
    /// Human-readable error message (always present).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    //public IDictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents an individual non resut error entry.
/// </summary>
public class ProblemError
{
    //public IDictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
}