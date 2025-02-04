// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using System.Diagnostics;
using DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, UserId={UserId}, Plan={Plan}")]
[TypedEntityId<Guid>]
public class Subscription : AuditableAggregateRoot<SubscriptionId>, IConcurrency
{
    public string UserId { get; set; }

    public SubscriptionPlan Plan { get; set; }

    public SubscriptionStatus Status { get; set; }

    public SubscriptionBillingCycle BillingCycle { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive => this.Status == SubscriptionStatus.Active &&
                           (this.EndDate == null || this.EndDate > DateTime.UtcNow);

    public Guid ConcurrencyVersion { get; set; }
}
