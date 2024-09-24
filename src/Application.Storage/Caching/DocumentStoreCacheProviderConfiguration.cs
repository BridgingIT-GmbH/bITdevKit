// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

public class DocumentStoreCacheProviderConfiguration
{
    public TimeSpan? SlidingExpiration { get; set; }

    public DateTimeOffset? AbsoluteExpiration { get; set; }

    public string ConnectionString { get; set; }
}