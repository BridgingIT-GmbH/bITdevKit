// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class EnumerationModel
{
    public IEnumerable<TodoStatus> TodoStatuses { get; set; } = TodoStatus.GetAll();

    public IEnumerable<TodoPriority> TodoPriorities { get; set; } = TodoPriority.GetAll();

    public IEnumerable<SubscriptionStatus> SubscriptionStatuses { get; set; } = SubscriptionStatus.GetAll();

    public IEnumerable<SubscriptionPlan> SubscriptionPlans { get; set; } = SubscriptionPlan.GetAll();

    public IEnumerable<SubscriptionBillingCycle> SubscriptionBillingCycles { get; set; } = SubscriptionBillingCycle.GetAll();
}