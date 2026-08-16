// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides a mutable transport envelope for a non-generic <see cref="Result"/>.</summary>
public class ResultWrapper
{
    /// <summary>Gets or sets the enveloped result.</summary>
    public Result Result { get; set; }
}

/// <summary>Provides a mutable transport envelope for a value <see cref="Result{T}"/>.</summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public class ResultWrapper<T>
{
    /// <summary>Gets or sets the enveloped value result.</summary>
    public Result<T> Result { get; set; }
}

/// <summary>Provides a mutable transport envelope for a <see cref="ResultPaged{T}"/>.</summary>
/// <typeparam name="T">The type of item in the paged result.</typeparam>
public class ResultPagedWrapper<T>
{
    /// <summary>Gets or sets the enveloped paged result.</summary>
    public ResultPaged<T> Result { get; set; }
}
