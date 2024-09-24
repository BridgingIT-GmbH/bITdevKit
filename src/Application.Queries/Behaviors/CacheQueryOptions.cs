// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

public interface ICacheQuery
{
    CacheQueryOptions Options { get; }
}

public class CacheQueryOptions
{
    public string Key { get; set; }

    public TimeSpan? SlidingExpiration { get; set; }

    public DateTimeOffset? AbsoluteExpiration { get; set; }
}