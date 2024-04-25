// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using System.Security.Claims;

public class FakeAuthenticationHandlerOptions
{
    private List<Claim> claims = new();

    public IEnumerable<Claim> Claims { get => this.claims; set => this.claims = value.ToList(); }

    public FakeAuthenticationHandlerOptions AddClaim(string type, string value)
    {
        if (type.IsNullOrEmpty())
        {
            return this;
        }

        this.claims.Add(new Claim(type, value));

        return this;
    }

    public FakeAuthenticationHandlerOptions AddClaims(string type, IEnumerable<string> values)
    {
        if (type.IsNullOrEmpty())
        {
            return this;
        }

        foreach (var value in values.SafeNull())
        {
            this.claims.Add(new Claim(type, value));
        }

        return this;
    }
}