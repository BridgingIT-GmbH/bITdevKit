// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Mvc;
using System;
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

    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp1 { get; set; } = string.Empty;

    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp2 { get; set; } = string.Empty;

    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp3 { get; set; } = string.Empty;
}

/// <summary>
/// Represents an individual non resut error entry.
/// </summary>
public class ProblemError
{
    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp1 { get; set; } = string.Empty;

    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp2 { get; set; } = string.Empty;

    /// <summary>
    /// Extra properties providing additional context for the error.
    /// </summary>
    public string AdditionalProp3 { get; set; } = string.Empty;
}