# Review Summary Template

Use this template to summarize code review findings at the end of a review. It provides a clear overview of issues found, prioritized action items, and next steps.

## Template

```markdown
# Code Review Summary

**Reviewer**: {Your Name}  
**Date**: {YYYY-MM-DD}  
**PR/Branch**: {PR number or branch name}  
**Files Reviewed**: {Number of files}

## Issues Found

- 🔴 **CRITICAL**: {count} (must fix before merge)
- 🟡 **IMPORTANT**: {count} (should address, discuss if deferring)
- 🟢 **SUGGESTION**: {count} (optional improvements)

**Total**: {count} issues

## Top 3 Priorities

1. **[PRIORITY] Category**: {Brief description}
   - Location: {File:Line}
   - Impact: {Why this is a priority}
   - Action: {What needs to be done}

2. **[PRIORITY] Category**: {Brief description}
   - Location: {File:Line}
   - Impact: {Why this is a priority}
   - Action: {What needs to be done}

3. **[PRIORITY] Category**: {Brief description}
   - Location: {File:Line}
   - Impact: {Why this is a priority}
   - Action: {What needs to be done}

## Overall Assessment

{1-2 sentences on overall code quality, positive aspects, and main areas for improvement}

## Next Steps

- [ ] {Action item 1}
- [ ] {Action item 2}
- [ ] {Action item 3}
- [ ] {Action item 4}

## Additional Notes

{Any additional context, concerns, or suggestions}
```

---

## Example 1: Review with Critical Issues

```markdown
# Code Review Summary

**Reviewer**: Code Reviewer  
**Date**: 2024-01-15  
**PR/Branch**: PR #123 - Add Customer Management Feature  
**Files Reviewed**: 15 files

## Issues Found

- 🔴 **CRITICAL**: 3 (must fix before merge)
- 🟡 **IMPORTANT**: 5 (should address, discuss if deferring)
- 🟢 **SUGGESTION**: 8 (optional improvements)

**Total**: 16 issues

## Top 3 Priorities

1. **🔴 CRITICAL - Security: Hardcoded API Key**
   - Location: `EmailService.cs:42`
   - Impact: API key exposed in source code and version control. This is a security breach that could allow unauthorized access to the email service.
   - Action: Remove hardcoded API key and use `IConfiguration` to load from Azure Key Vault or environment variables.

2. **🔴 CRITICAL - Performance: Blocking on Async Code**
   - Location: `CustomerController.cs:89`
   - Impact: Using `.Result` on async method can cause deadlocks in ASP.NET Core contexts, potentially hanging the entire application under load.
   - Action: Change method to async and await the call properly.

3. **🟡 IMPORTANT - Testing: No Tests for New Feature**
   - Location: `Customer.cs:Activate()` method
   - Impact: New business logic has no tests. This means regressions could go undetected and the behavior is not verified.
   - Action: Add unit tests covering happy path, already active scenario, and missing email case.

## Overall Assessment

Good overall structure following Clean Architecture and DDD patterns. The domain model is well-designed with proper encapsulation and business rules. However, there are critical security and performance issues that must be addressed before merge. Test coverage needs improvement for new functionality.

## Next Steps

- [ ] **CRITICAL**: Remove hardcoded API key from `EmailService.cs` and use configuration
- [ ] **CRITICAL**: Fix blocking async calls in `CustomerController.cs` - make methods async
- [ ] **CRITICAL**: Verify no other hardcoded secrets (search for "password", "apikey", "secret")
- [ ] **IMPORTANT**: Add unit tests for `Customer.Activate()` method
- [ ] **IMPORTANT**: Fix N+1 query in `CustomerService.GetCustomersWithOrders()`
- [ ] **IMPORTANT**: Add `.AsNoTracking()` to read-only queries in `CustomerRepository`
- [ ] Run `dotnet format` to fix .editorconfig violations
- [ ] Verify all CRITICAL issues resolved before requesting re-review

## Additional Notes

**Positive aspects**:
- Excellent use of Result<T> pattern for error handling
- Good domain encapsulation with private setters and factory methods
- Clear naming conventions throughout

**Areas for improvement beyond this PR**:
- Consider adding integration tests for customer endpoints
- XML documentation on public APIs could be more comprehensive
- Look into caching frequently accessed customer data

**Resources**:
- Security examples: `.github/skills/review-code/examples/security-examples.md`
- Performance checklist: `.github/skills/review-code/checklists/04-performance.md`
- Testing guide: `.github/skills/review-code/checklists/03-testing.md`
```

---

## Example 2: Review with Only Minor Issues

```markdown
# Code Review Summary

**Reviewer**: Code Reviewer  
**Date**: 2024-01-15  
**PR/Branch**: PR #456 - Refactor Customer Query Logic  
**Files Reviewed**: 6 files

## Issues Found

- 🔴 **CRITICAL**: 0 (must fix before merge)
- 🟡 **IMPORTANT**: 1 (should address, discuss if deferring)
- 🟢 **SUGGESTION**: 5 (optional improvements)

**Total**: 6 issues

## Top 3 Priorities

1. **🟡 IMPORTANT - Performance: Missing AsNoTracking**
   - Location: `CustomerQueryHandler.cs:34-38`
   - Impact: Read-only queries are tracking entities unnecessarily, consuming memory and reducing performance.
   - Action: Add `.AsNoTracking()` before `.ToListAsync()` on lines 38.

2. **🟢 SUGGESTION - Documentation: Missing XML Comments**
   - Location: `CustomerQueryHandler.cs:24`
   - Impact: Public handler has no XML documentation. Consumers don't get IntelliSense help.
   - Action: Add `<summary>`, `<param>`, and `<returns>` XML comments.

3. **🟢 SUGGESTION - Readability: Complex LINQ Expression**
   - Location: `CustomerQueryHandler.cs:52-58`
   - Impact: 7-line LINQ expression is hard to read. Could be more maintainable.
   - Action: Consider extracting to a specification class or breaking into intermediate variables.

## Overall Assessment

Excellent refactoring that improves code organization and maintainability. The new query structure is much cleaner and follows the specification pattern well. Only minor issues to address, mostly related to performance optimization and documentation. Great work!

## Next Steps

- [ ] **IMPORTANT**: Add `.AsNoTracking()` to read-only queries in `CustomerQueryHandler.cs`
- [ ] **SUGGESTION**: Add XML documentation to public handler class
- [ ] **SUGGESTION**: Consider extracting complex LINQ to specification class
- [ ] Verify tests pass after AsNoTracking change
- [ ] Ready to merge after IMPORTANT issue addressed

## Additional Notes

**Positive aspects**:
- Clean separation of query concerns
- Excellent use of specification pattern
- Good test coverage (85% for new code)
- Clear naming and structure

**Nice improvements**:
- This refactoring makes it much easier to add new query variations
- Performance should improve with the AsNoTracking change
- Code is more maintainable and testable

**Future enhancements** (not for this PR):
- Consider adding query result caching for frequently accessed data
- Pagination support would be beneficial for large result sets
```

---

## Example 3: Review with Mixed Issues

```markdown
# Code Review Summary

**Reviewer**: Code Reviewer  
**Date**: 2024-01-15  
**PR/Branch**: PR #789 - Add Order Processing Feature  
**Files Reviewed**: 22 files

## Issues Found

- 🔴 **CRITICAL**: 2 (must fix before merge)
- 🟡 **IMPORTANT**: 7 (should address, discuss if deferring)
- 🟢 **SUGGESTION**: 12 (optional improvements)

**Total**: 21 issues

## Top 3 Priorities

1. **🔴 CRITICAL - Correctness: Missing Transaction for Multi-Step Operation**
   - Location: `OrderService.cs:145-167`
   - Impact: Creating order, updating inventory, and charging payment happen without a transaction. If any step fails, data will be in inconsistent state (order created but payment not charged, or payment charged but inventory not updated).
   - Action: Wrap the three operations in a database transaction or use the Unit of Work pattern.

2. **🔴 CRITICAL - Code Style: Block-Scoped Namespace Violations**
   - Location: `Order.cs`, `OrderItem.cs`, `Payment.cs` (6 files total)
   - Impact: These files use block-scoped namespace syntax, violating the `.editorconfig` error-level rule. This will fail CI/CD build.
   - Action: Convert to file-scoped namespaces. Run `dotnet format` to auto-fix.

3. **🟡 IMPORTANT - Testing: Low Test Coverage for Critical Business Logic**
   - Location: `OrderService.cs:ProcessOrder()` method
   - Impact: The core order processing logic has only 45% test coverage. Critical error paths and edge cases are not tested.
   - Action: Add tests for: payment failure, insufficient inventory, invalid order state, concurrent order processing.

## Overall Assessment

Solid implementation of order processing with good domain modeling. The aggregate structure is well-thought-out and follows DDD principles. However, there are critical correctness issues (missing transaction) and code style violations that must be fixed before merge. Test coverage needs significant improvement for such critical functionality.

## Next Steps

**CRITICAL (must fix before merge)**:
- [ ] Wrap order processing in database transaction (`OrderService.cs:145-167`)
- [ ] Convert all domain files to file-scoped namespaces (run `dotnet format`)
- [ ] Verify transaction rollback works correctly when any step fails

**IMPORTANT (should address)**:
- [ ] Add tests for payment failure scenario
- [ ] Add tests for insufficient inventory scenario
- [ ] Add tests for invalid order state transitions
- [ ] Add tests for concurrent order processing (optimistic concurrency)
- [ ] Add proper logging for order processing steps
- [ ] Add cancellation token support throughout order processing
- [ ] Document order processing workflow in XML comments

**SUGGESTION (optional improvements)**:
- [ ] Consider extracting payment logic to separate PaymentService
- [ ] Add order status change notification (domain events)
- [ ] Consider adding order processing metrics/monitoring
- [ ] XML documentation on public APIs

## Additional Notes

**Architecture concerns**:
The lack of transaction is a significant issue. Consider using:
1. Database transaction (wrap in `TransactionScope` or EF Core transaction)
2. Unit of Work pattern to manage transaction across multiple aggregates
3. Saga pattern if operations span multiple services/databases

**Testing gaps**:
Current tests only cover happy path. Need tests for:
- Payment gateway failures
- Network timeouts during payment
- Inventory reservation conflicts
- Invalid order states
- Concurrent modifications

**Performance considerations**:
- Order processing involves multiple database round-trips. Consider bulk operations for order items.
- Payment processing is synchronous. Consider async/background processing for improved responsiveness.

**Resources**:
- Transaction patterns: See project ADR-0007 (EF Core Code-First Migrations)
- Testing examples: `.github/skills/review-code/examples/testing-examples.md`
- .editorconfig compliance: `.github/skills/review-code/examples/editorconfig-compliance.md`
```

---

## Summary

Review summary template provides:

✅ **Clear overview** (issue count by priority)  
✅ **Top 3 priorities** (most important issues to address)  
✅ **Overall assessment** (balanced view of code quality)  
✅ **Actionable next steps** (prioritized checklist)  
✅ **Additional context** (positive aspects, future enhancements)  

**When to use**:
- At the end of every code review
- After reviewing all files and leaving inline comments
- Before requesting changes or approving PR

**Benefits**:
- Author knows exactly what needs to be fixed
- Priorities are clear (CRITICAL vs IMPORTANT vs SUGGESTION)
- Positive aspects are acknowledged
- Next steps are actionable and specific

**Tips**:
- Keep "Top 3 Priorities" focused on most impactful issues
- Balance criticism with recognition of good work
- Make "Next Steps" specific and checkable
- Include links to resources for learning
