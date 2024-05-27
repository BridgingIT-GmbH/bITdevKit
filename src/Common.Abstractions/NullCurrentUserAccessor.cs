// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class NullCurrentUserAccessor : ICurrentUserAccessor
{
    public string UserId => null;

    public string UserName => null;

    public string Email => null;

    public string[] Roles => null;
}