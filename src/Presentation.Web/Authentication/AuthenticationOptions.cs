// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;

public class AuthenticationOptions
{
    public string Authority { get; set; }

    public string ValidIssuer { get; set; }

    public bool ValidateIssuer { get; set; }

    public string ValidAudience { get; set; }

    public bool ValidateAudience { get; set; }

    public bool ValidateLifetime { get; set; }

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    public bool RequireHttpsMetadata { get; set; }

    public bool SaveToken { get; set; }

    public string SigningKey { get; set; }

    public bool ValidateSigningKey { get; set; }

    public bool RequireSignedTokens { get; set; }
}