// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemByUserSpecification(string userId) : Specification<TodoItem>
{
    public override Expression<Func<TodoItem, bool>> ToExpression()
    {
        return item => item.UserId == userId;
    }
}