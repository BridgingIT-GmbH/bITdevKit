// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

public interface ICacheInvalidateCommand
{
    CacheInvalidateCommandOptions Options { get; }
}

public class CacheInvalidateCommandOptions
{
    public string Key { get; set; }
}