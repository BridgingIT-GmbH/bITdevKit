// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain;

using System.Linq.Expressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemIsNotDeletedSpecification : Specification<TodoItem>
{
    public override Expression<Func<TodoItem, bool>> ToExpression()
    {
        return e => e.AuditState == null || e.AuditState.Deleted == null || e.AuditState.Deleted == false;
    }
}
