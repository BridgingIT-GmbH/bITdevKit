// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

public static class SpecificationExtensions
{
    public static bool IsSatisfiedBy<T>(IEnumerable<ISpecification<T>> source, T entity)
    {
        if (source?.Any() == true)
        {
            foreach (var specification in source)
            {
                if (!specification.IsSatisfiedBy(entity))
                {
                    return false;
                }
            }
        }

        return true;
    }
}