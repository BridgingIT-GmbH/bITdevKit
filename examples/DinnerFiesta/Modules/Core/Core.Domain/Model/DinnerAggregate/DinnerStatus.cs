// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerStatus(int id, string name) : Enumeration(id, name)
{
    public static DinnerStatus Draft = new(0, nameof(Draft));
    public static DinnerStatus Upcoming = new(1, nameof(Upcoming));
    public static DinnerStatus InProgress = new(2, nameof(InProgress));
    public static DinnerStatus Ended = new(3, nameof(Ended));
    public static DinnerStatus Cancelled = new(3, nameof(Cancelled));

    public static IEnumerable<DinnerStatus> GetAll()
    {
        return GetAll<DinnerStatus>();
    }
}