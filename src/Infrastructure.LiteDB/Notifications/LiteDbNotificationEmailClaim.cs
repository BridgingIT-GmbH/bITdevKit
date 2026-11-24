// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Notifications;

/// <summary>Retains immutable delivery ownership independently of mutable notification properties.</summary>
/// <param name="Message">The detached email passed to the configured notification service.</param>
/// <param name="Token">The unique token required for renewing this claim.</param>
public sealed record LiteDbNotificationEmailClaim(EmailMessage Message, string Token);
