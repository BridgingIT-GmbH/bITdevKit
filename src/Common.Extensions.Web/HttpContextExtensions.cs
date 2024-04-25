// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    private const string CorrelationKey = "CorrelationId";

    public static string TryGetCorrelationId(this HttpContext context)
        => context.Items.TryGetValue(CorrelationKey, out var id) ? id.ToString() : null;
}