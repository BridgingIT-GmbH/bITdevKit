// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using BridgingIT.DevKit.Common.Options;
using BridgingIT.DevKit.Domain.Repositories;

public interface ILiteDbRepositoryOptions : IRepositoryOptions, ILoggerOptions
{
    ILiteDbContext DbContext { get; set; }
}