// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;

using DevKit.Domain.Model;

public class UserAccount : AggregateRoot<Guid>
{
    public string Email { get; set; }

    public int VisitCount { get; set; }

    public DateTimeOffset? LastVisitDate { get; set; }

    public DateTimeOffset? RegisterDate { get; set; }

    public AdAccount AdAccount { get; set; }
}