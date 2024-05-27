// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public interface ICurrentUserAccessor
{
    public string UserId { get; }

    public string UserName { get; }

    public string Email { get; }

    public string[] Roles { get; }
}