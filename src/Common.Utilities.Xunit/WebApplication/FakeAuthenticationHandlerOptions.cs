// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Claims;

/// <summary>
/// Configuration options for the FakeAuthenticationHandler.
/// </summary>
/// <remarks>
/// This class allows the customization of the behavior of the FakeAuthenticationHandler
/// by providing a way to specify claims that should be included in the authentication ticket.
/// </remarks>
public class FakeAuthenticationHandlerOptions
{
    /// <summary>
    /// A list of claims that are used in the FakeAuthenticationHandlerOptions.
    /// </summary>
    private List<Claim> claims = [];

    /// <summary>
    /// Gets or sets the collection of claims associated with the FakeAuthenticationHandlerOptions.
    /// </summary>
    /// <value>
    /// An IEnumerable of Claim objects that represent the claims to be included in the
    /// authentication process. The setter converts the input value to a list.
    /// </value>
    public IEnumerable<Claim> Claims
    {
        get => this.claims;
        set => this.claims = value.ToList();
    }

    /// Adds a claim to the FakeAuthenticationHandlerOptions.
    /// <param name="type">The type of the claim.</param>
    /// <param name="value">The value of the claim.</param>
    /// <return>Returns the current FakeAuthenticationHandlerOptions instance.</return>
    public FakeAuthenticationHandlerOptions AddClaim(string type, string value)
    {
        if (type.IsNullOrEmpty())
        {
            return this;
        }

        this.claims.Add(new Claim(type, value));

        return this;
    }

    /// <summary>
    /// Adds claims to the FakeAuthenticationHandlerOptions.
    /// </summary>
    /// <param name="type">The type of the claim.</param>
    /// <param name="values">A collection of values for the claim.</param>
    /// <returns>The FakeAuthenticationHandlerOptions instance with added claims.</returns>
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