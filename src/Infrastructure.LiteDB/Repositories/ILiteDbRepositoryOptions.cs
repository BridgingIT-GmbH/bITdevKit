// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common.Options;
using Domain.Repositories;

/// <summary>
/// Defines operations for i lite db repository options.
/// </summary>
public interface ILiteDbRepositoryOptions : IRepositoryOptions, ILoggerOptions
{
    /// <summary>
    /// Gets or sets the db context.
    /// </summary>
    ILiteDbContext DbContext { get; set; }
}
