// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.EntityFramework;

public class DbUserAccount
{
    public Guid Identifier { get; set; }

    public string EmailAddress { get; set; }

    public int Visits { get; set; }

    public DateTimeOffset? LastVisitDate { get; set; }

    public DateTimeOffset? RegisterDate { get; set; }

    public string AdDomain { get; set; }

    public string AdName { get; set; }
}