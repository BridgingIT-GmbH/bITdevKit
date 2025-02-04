// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

public class SubscriptionModel
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public int Plan { get; set; }

    public int Status { get; set; }

    public int BillingCycle { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string ConcurrencyVersion { get; set; }
}
