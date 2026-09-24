---
created: 2026-09-24
status: draft
---

# Specification: C# Static Analysis Triage

> This specification defines a controlled campaign for classifying and resolving C# static-analysis findings in hand-maintained source under `src/`, `tests/`, `examples/`, and `benchmarks/`. It establishes a reproducible baseline, deterministic classification, compatibility safeguards, reviewable batches, and validation gates. It is a triage specification and does not authorize bulk fixes of every reported finding.

[TOC]

## Introduction

JetBrains InspectCode reports 9,050 findings in the verified final baseline, while the full solution build completes with 0 warnings and 0 errors. The difference is expected because InspectCode applies inspections beyond compiler diagnostics and can report model-loading limitations. Its `error` and `warning` levels are analyzer severity labels; they do not prove that the C# compiler or solution build failed.

This specification converts that broad inventory into bounded engineering work. Every finding must be reproduced, classified, prioritized, and assigned a disposition before source changes are made. Findings must be processed in small batches that preserve public compatibility and provide reviewable validation evidence.

## Status and Baseline

Status: **Draft**

The verified baseline source is `%TEMP%\bitdevkit-inspectcode-final-after-all-edits.sarif`.

| Measure | Count |
| --- | ---: |
| Total findings | 9,050 |
| Analyzer severity `error` | 354 |
| Analyzer severity `warning` | 8,696 |
| Full solution build warnings | 0 |
| Full solution build errors | 0 |
| Roslyn style diagnostics from the prior 4,710-file verification | 0 |

Baseline findings by repository root:

| Root | Count |
| --- | ---: |
| `src/` | 6,788 |
| `tests/` | 1,819 |
| `examples/` | 426 |
| `benchmarks/` | 17 |
| **Total** | **9,050** |

The custom naming rules below report zero findings and must remain at zero:

* `private_constants_rule`
* `private_instance_fields_rule`
* `private_static_fields_rule`
* `private_static_readonly_rule`

This baseline is a planning snapshot, not an allowlist or a fixed completion target. Every implementation batch must regenerate the report and record its own before-and-after counts.

## Goals

### Produce an actionable inventory

Every in-scope finding must have a stable fingerprint, deterministic family, priority, disposition, and current status in the triage ledger.

### Resolve material risks first

Work must prioritize reproducible compiler failures, disposal and concurrency defects, and correctness defects before lower-risk cleanup.

### Preserve compatibility and behavior

Changes must retain public API and behavioral compatibility unless a separately approved change explicitly defines and validates a compatibility break.

### Deliver reviewable changes

Each batch must address one cohesive project, feature, rule, or risk category and remain small enough for semantic review.

### Maintain a reproducible baseline

Each completed batch must produce a fresh InspectCode report, reconciled counts, and validation evidence.

## Non-Goals

This campaign will not:

* authorize bulk fixing of all 9,050 findings
* treat analyzer severity as proof of runtime impact or compiler failure
* rename or remove public APIs in bulk
* suppress findings solely to reduce counts
* edit generated, scaffolded, migration, designer, or build-output files
* require every advisory style inspection to be enabled or fixed
* combine unrelated refactoring, formatting, or dependency updates with triage batches
* guarantee that the baseline remains numerically unchanged while unrelated repository work continues
* make temporary JetBrains tooling a repository dependency

## Scope

### Included

The campaign includes hand-maintained C# source under:

```text
src/**/*.cs
tests/**/*.cs
examples/**/*.cs
benchmarks/**/*.cs
```

An included file must be authored and maintained as repository source rather than produced by a compiler, generator, scaffold, designer, migration tool, or build.

### Excluded

The following files and paths are excluded before family assignment:

* `**/bin/**`
* `**/obj/**`
* generated API clients
* source-generated Razor C# and Razor intermediate output
* designer-generated files, including `*.Designer.cs` and `*.designer.cs`
* Entity Framework migrations and model snapshots
* files whose names or contents identify them as generated, including `*.g.cs`, `*.g.i.cs`, `*.generated.cs`, and files containing standard auto-generated markers

No location in the verified SARIF baseline matched these generated-path exclusions. InspectCode emitted generated Razor loading warnings on standard error; those warnings are tool noise outside SARIF and must not be entered as source findings.

### Scope decision requirements

* **SCP-001**: Generated and external path exclusions must be applied before all other classification rules.
* **SCP-002**: A finding without a source location must remain eligible for compiler/model-loading classification or manual review.
* **SCP-003**: Ambiguous generated-code status must be resolved before a source edit is authorized.
* **SCP-004**: Excluded findings must remain visible in exclusion evidence so that filtering is auditable.

## Compatibility and Change Constraints

* **CON-001**: The campaign must not perform bulk public renames, removals, signature changes, visibility reductions, default-value changes, or nullability-contract changes.
* **CON-002**: Every proposed public or protected API change requires semantic review of callers, binary and source compatibility, serialization contracts, dependency injection registration, reflection use, and generated-code integration as applicable.
* **CON-003**: Analyzer quick-fixes must not be applied without reviewing their complete diff and behavioral consequences.
* **CON-004**: A suppression must identify why the finding is intentional, safe, and narrower than changing or disabling the rule globally.
* **CON-005**: Rule configuration changes require evidence that the rule is systematically unsuitable or incorrectly scoped; current count alone is insufficient.
* **CON-006**: Unrelated formatting and refactoring are prohibited in a triage batch.
* **CON-007**: Top-level solution builds and tests must run sequentially in the same worktree.
* **CON-008**: A compiler/model-loading finding must first be reproduced with the compiler or editor before it can be classified as a source defect.

## Baseline Findings

### Top rules

| Rule | Count |
| --- | ---: |
| `InvalidXmlDocComment` | 1,708 |
| `InconsistentNaming` | 1,156 |
| `UnusedAutoPropertyAccessor.Global` | 1,077 |
| `PossibleMultipleEnumeration` | 686 |
| `UnusedParameter.Local` | 590 |
| `ParameterHidesMember` | 359 |
| `PossibleNullReferenceException` | 294 |
| `.CSharpErrors` | 270 |
| `RedundantUsingDirective` | 205 |
| `UnusedMember.Local` | 194 |
| `ConstantConditionalAccessQualifier` | 193 |
| `LocalVariableHidesMember` | 172 |
| `UnusedVariable` | 168 |
| `AssignNullToNotNullAttribute` | 156 |
| `NotAccessedPositionalProperty.Global` | 147 |
| `ExpressionIsAlwaysNull` | 144 |
| `UnusedAutoPropertyAccessor.Local` | 102 |
| `PossibleInvalidOperationException` | 93 |
| `ConditionIsAlwaysTrueOrFalse` | 92 |
| `ArrangeThisQualifier` | 78 |
| `ConvertTypeCheckPatternToNullCheck` | 71 |
| `RedundantAssignment` | 69 |
| `VariableHidesOuterVariable` | 66 |
| `RedundantCast` | 62 |
| `RedundantDefaultMemberInitializer` | 61 |
| `NotAccessedPositionalProperty.Local` | 59 |
| `SuspiciousTypeConversion.Global` | 53 |
| `AccessToDisposedClosure` | 39 |
| `RedundantTypeArgumentsOfMethod` | 33 |
| `NonReadonlyMemberInGetHashCode` | 31 |
| `NotAccessedField.Local` | 29 |
| `OptionalParameterHierarchyMismatch` | 29 |
| `PossiblyMistakenUseOfCancellationToken` | 27 |
| `RedundantCallerArgumentExpressionDefaultValue` | 26 |
| `PrivateFieldCanBeConvertedToLocalVariable` | 26 |
| `CapturedPrimaryConstructorParameterIsMutable` | 21 |
| `ConstantNullCoalescingCondition` | 20 |
| `HeuristicUnreachableCode` | 20 |
| `RedundantExtendsListEntry` | 20 |
| `InheritdocInvalidUsage` | 18 |
| `PossibleUnintendedReferenceComparison` | 17 |
| `UnusedTypeParameter` | 16 |
| `RedundantJumpStatement` | 16 |
| `Html.PathError` | 14 |
| `UsingStatementResourceInitialization` | 13 |
| `MemberHidesStaticFromOuterClass` | 12 |
| `RedundantUsingDirective.Global` | 12 |
| `UnusedMethodReturnValue.Local` | 12 |
| `CollectionNeverUpdated.Local` | 11 |
| `CollectionNeverQueried.Local` | 11 |
| `ParameterHidesPrimaryConstructorParameter` | 11 |
| `InconsistentlySynchronizedField` | 11 |
| `ReturnValueOfPureMethodIsNotUsed` | 11 |
| `RedundantEnumerableCastCall` | 10 |
| `PartialTypeWithSinglePart` | 10 |
| `VirtualMemberCallInConstructor` | 10 |
| `AccessToModifiedClosure` | 9 |
| `StaticMemberInGenericType` | 9 |
| `MethodOverloadWithOptionalParameter` | 9 |
| `NotResolvedInText` | 9 |

Rules not listed in this table remain part of the 9,050-finding baseline and must be represented through the family totals and ledger.

## Classification Model

### Deterministic family precedence

Each SARIF finding must be assigned to exactly one family. Classification must use the following precedence and stop at the first match:

1. generated or external path exclusion
2. compiler/model-loading
3. documentation
4. disposal/async/concurrency
5. performance/allocation
6. dead or unused code
7. correctness/nullability/control-flow
8. API/design/encapsulation
9. naming/spelling/style
10. fallback manual classification

Precedence is required because a rule or location may satisfy more than one conceptual category. Generated-path matches are excluded before the nine in-SARIF families are totaled. The baseline has zero such matches, so the mutually exclusive family counts reconcile exactly to 9,050.

* **CLS-001**: Every SARIF finding must map to exactly one in-scope family or one documented exclusion.
* **CLS-002**: Classification must use the precedence above without per-finding reordering.
* **CLS-003**: Unknown rules must enter `Other/manual classification` until an explicit deterministic mapping is approved.
* **CLS-004**: Family mappings must be version-controlled with the campaign implementation or recorded in the ledger procedure.

### Family totals

| Family | Total | Error | Warning |
| --- | ---: | ---: | ---: |
| Dead or unused code | 2,449 | 0 | 2,449 |
| Documentation | 1,740 | 0 | 1,740 |
| Naming/spelling/style | 1,705 | 78 | 1,627 |
| Correctness/nullability/control-flow | 1,236 | 0 | 1,236 |
| API/design/encapsulation | 729 | 0 | 729 |
| Performance/allocation | 696 | 0 | 696 |
| Compiler/model-loading | 282 | 270 | 12 |
| Other/manual classification | 127 | 6 | 121 |
| Disposal/async/concurrency | 86 | 0 | 86 |
| **Total** | **9,050** | **354** | **8,696** |

### Root by family

| Family | `benchmarks/` | `examples/` | `src/` | `tests/` | Total |
| --- | ---: | ---: | ---: | ---: | ---: |
| API/design/encapsulation | 0 | 5 | 514 | 210 | 729 |
| Compiler/model-loading | 0 | 0 | 275 | 7 | 282 |
| Correctness/nullability/control-flow | 0 | 32 | 863 | 341 | 1,236 |
| Dead or unused code | 17 | 232 | 1,505 | 695 | 2,449 |
| Disposal/async/concurrency | 0 | 0 | 50 | 36 | 86 |
| Documentation | 0 | 2 | 1,730 | 8 | 1,740 |
| Naming/spelling/style | 0 | 113 | 1,460 | 132 | 1,705 |
| Other/manual classification | 0 | 11 | 79 | 37 | 127 |
| Performance/allocation | 0 | 31 | 312 | 353 | 696 |
| **Total** | **17** | **426** | **6,788** | **1,819** | **9,050** |

### Representative family rules

| Family | Representative rules and baseline counts |
| --- | --- |
| API/design/encapsulation | `ParameterHidesMember` 359; `LocalVariableHidesMember` 172; `VariableHidesOuterVariable` 66; `OptionalParameterHierarchyMismatch` 29; `PrivateFieldCanBeConvertedToLocalVariable` 26; `CapturedPrimaryConstructorParameterIsMutable` 21; `MemberHidesStaticFromOuterClass` 12; `ParameterHidesPrimaryConstructorParameter` 11 |
| Compiler/model-loading | `.CSharpErrors` 270; `NotResolvedInText` 9; `Html.AttributeValueNotResolved` 3 |
| Correctness/nullability/control-flow | `PossibleNullReferenceException` 294; `ConstantConditionalAccessQualifier` 193; `AssignNullToNotNullAttribute` 156; `ExpressionIsAlwaysNull` 144; `PossibleInvalidOperationException` 93; `ConditionIsAlwaysTrueOrFalse` 92; `ConvertTypeCheckPatternToNullCheck` 71; `RedundantAssignment` 69 |
| Dead or unused code | `UnusedAutoPropertyAccessor.Global` 1,077; `UnusedParameter.Local` 590; `UnusedMember.Local` 194; `UnusedVariable` 168; `NotAccessedPositionalProperty.Global` 147; `UnusedAutoPropertyAccessor.Local` 102; `NotAccessedPositionalProperty.Local` 59; `NotAccessedField.Local` 29 |
| Disposal/async/concurrency | `AccessToDisposedClosure` 39; `PossiblyMistakenUseOfCancellationToken` 27; `InconsistentlySynchronizedField` 11; `AccessToModifiedClosure` 9 |
| Documentation | `InvalidXmlDocComment` 1,708; `InheritdocInvalidUsage` 18; `Html.PathError` 14 |
| Naming/spelling/style | `InconsistentNaming` 1,156; `RedundantUsingDirective` 205; `ArrangeThisQualifier` 78; `RedundantCast` 62; `RedundantDefaultMemberInitializer` 61; `RedundantTypeArgumentsOfMethod` 33; `RedundantCallerArgumentExpressionDefaultValue` 26; `RedundantExtendsListEntry` 20 |
| Performance/allocation | `PossibleMultipleEnumeration` 686; `RedundantEnumerableCastCall` 10 |
| Other/manual classification | `NonReadonlyMemberInGetHashCode` 31; `UsingStatementResourceInitialization` 13; `CompareOfFloatsByEqualityOperator` 7; `ExplicitCallerInfoArgument` 7; `UnassignedGetOnlyAutoProperty` 6; `LocalizableElement` 6; `.RazorErrors` 6; `MustUseReturnValue` 5 |

Representative rules are examples, not complete family definitions.

## Priority Model

Priority is assigned after reproduction and family classification. Analyzer severity alone must not determine priority.

### P0: confirmed immediate failure or critical defect

A finding is P0 only when evidence confirms at least one of the following:

* the compiler or a supported editor reproduces a blocking source error
* a deterministic defect can cause data loss, security failure, deadlock, resource exhaustion, or broad production unavailability
* the finding identifies a release-blocking regression in supported behavior

Compiler/model-loading findings are not automatically P0 because the full solution build is clean. They must first be reproduced with the compiler or editor. A JetBrains model-loading error that cannot be independently reproduced is `Needs-tooling`, not source P0.

### P1: high-confidence correctness or lifecycle risk

P1 includes reproducible disposal, async, concurrency, nullability, control-flow, or API-contract defects with meaningful runtime impact but without demonstrated P0 impact.

### P2: maintainability, performance, and localized design risk

P2 includes measured or strongly supported performance defects, misleading API or encapsulation problems, invalid documentation that affects consumers, and dead code whose removal has been semantically verified.

### P3: low-risk cleanup or advisory improvement

P3 includes style, naming, spelling, redundant syntax, low-impact documentation cleanup, and findings with limited operational consequence.

### Excluded and Needs-tooling

`Excluded` applies only to files outside scope under the generated and external exclusion policy. `Needs-tooling` applies when source analysis is blocked by model-loading failure, missing environment support, unresolved generated Razor input, or a tool limitation that prevents reliable source classification.

* **PRI-001**: Every in-scope ledger entry must have `P0`, `P1`, `P2`, `P3`, or `Needs-tooling` priority.
* **PRI-002**: Every out-of-scope generated or external entry must use `Excluded`.
* **PRI-003**: Priority rationale must cite observed impact or reproduction evidence, not only rule name or analyzer severity.
* **PRI-004**: A higher-priority unresolved finding must not be displaced by count-reduction work without a recorded dependency or risk decision.

## Dispositions

Every triaged finding must receive exactly one disposition:

| Disposition | Required meaning |
| --- | --- |
| `Fix` | Change hand-maintained source or documentation because the finding is valid and remediation is proportionate. |
| `Suppress with rationale` | Retain intentional behavior and add the narrowest maintainable suppression with a durable explanation. |
| `Configure/disable rule` | Change rule configuration because the rule is unsuitable for a defined scope; record affected scope and consequences. |
| `Exclude generated code` | Remove generated or external code from analysis without modifying that code. |
| `False positive/tool limitation` | Record evidence that the reported condition is not a source defect or cannot be modeled correctly by the analyzer. |
| `Defer` | Retain a valid finding for later work with explicit reason, owner or decision point, and review date or trigger. |

* **DSP-001**: `Suppress with rationale` must be local unless a broader suppression is independently justified.
* **DSP-002**: `Configure/disable rule` must identify the exact rule, scope, before-and-after count, and policy reason.
* **DSP-003**: `False positive/tool limitation` must include reproduction or counter-evidence.
* **DSP-004**: `Defer` must not be used as an unowned terminal state.
* **DSP-005**: A disposition may change when new evidence is recorded, but its history must remain auditable.

## Triage Ledger

The ledger is the authoritative campaign inventory. It may be implemented as a structured document, comma-separated values file, JSON file, or issue-backed store, provided that export preserves the required fields.

### Required schema

| Field | Type | Requirement |
| --- | --- | --- |
| `fingerprint` | string | Stable identifier derived from normalized rule, repository-relative path, symbol or region, and normalized message context. |
| `rule` | string | Exact SARIF rule identifier. |
| `message` | string | Original analyzer message without semantic rewriting. |
| `file` | string or null | Repository-relative path; null only when SARIF has no location. |
| `line` | integer or null | One-based start line from the report; null only when unavailable. |
| `root` | enum | `src`, `tests`, `examples`, `benchmarks`, `none`, or `excluded`. |
| `family` | enum | One of the nine mutually exclusive families or `generated/external exclusion`. |
| `priority` | enum | `P0`, `P1`, `P2`, `P3`, `Excluded`, or `Needs-tooling`. |
| `disposition` | enum | One of the six defined dispositions. |
| `rationale` | string | Evidence-based reason for priority and disposition. |
| `ownerBatch` | string | Responsible owner, issue, pull request, or batch identifier. |
| `status` | enum | `New`, `Triaged`, `In progress`, `Resolved`, `Suppressed`, `Deferred`, `Excluded`, or `Needs tooling`. |
| `firstSeen` | date-time | First report timestamp or campaign date containing the fingerprint. |
| `lastSeen` | date-time | Most recent report timestamp containing the fingerprint. |
| `validationEvidence` | string or list | Commands, test results, builds, report paths, count deltas, and review evidence. |

* **LED-001**: Fingerprints must remain stable when line numbers move without a semantic change.
* **LED-002**: A changed rule, file, symbol context, or materially changed message may create a new fingerprint.
* **LED-003**: Duplicate fingerprints in one report must be rejected or disambiguated deterministically.
* **LED-004**: Findings absent from a fresh report must not be marked resolved until relevant validation succeeds.
* **LED-005**: Reappearing fingerprints must retain their original `firstSeen` and update `lastSeen`.
* **LED-006**: Ledger aggregate counts must reconcile to the current filtered SARIF report.

## Delivery Strategy

All phases use small reviewable batches. A batch should normally contain one project or cohesive feature folder and one closely related rule set. Large mechanical counts do not justify large source diffs.

### Phase 1: establish exclusions and fingerprints

Define executable exclusion rules, fingerprint normalization, ledger schema, and baseline import. Confirm that all 9,050 SARIF findings are represented and that generated Razor standard-error messages remain separate tool-noise evidence.

### Phase 2: reconcile compiler and model-loading findings

Reproduce `.CSharpErrors`, unresolved text, HTML attribute resolution, Razor errors, and locationless findings with `dotnet build`, the supported editor, and focused project loading. Classify source defects separately from InspectCode model limitations.

### Phase 3: triage disposal, async, and concurrency

Review closure lifetime, cancellation-token use, synchronization, and modified closures. Prioritize by runtime reachability and impact.

### Phase 4: triage correctness, nullability, and control flow

Review possible null dereferences, invalid operations, impossible expressions, constant conditions, unintended comparisons, and redundant assignments. Require tests for behavior-changing fixes.

### Phase 5: triage documentation

Resolve invalid XML documentation, invalid `<inheritdoc/>`, and broken paths in coordination with the public API XML documentation policy. Do not rewrite meaningful documentation merely for consistency.

### Phase 6: triage performance and allocation

Review multiple enumeration and redundant enumerable casts. Confirm collection semantics, execution frequency, data size, and allocation impact before changing behavior.

### Phase 7: triage API, design, and encapsulation

Review member hiding, optional-parameter hierarchy mismatches, mutable captured primary-constructor parameters, and encapsulation suggestions. Public compatibility constraints take precedence over analyzer suggestions.

### Phase 8: triage dead and unused code

Prove non-use across reflection, serialization, dependency injection, source generation, tests, examples, and external public contracts before removal. Global analyzer labels are evidence, not proof of safe deletion.

### Phase 9: triage naming, style, and manual findings

Review naming and advisory cleanup after higher-risk work. Preserve the four zero-count custom naming rules. Manually classify fallback findings and retain only rules that provide useful repository signal.

## Batch Workflow

Every batch must follow this sequence:

1. Select a cohesive target and declare its files, rules, and expected finding fingerprints.
2. Generate a fresh pre-change InspectCode report.
3. Apply exclusions and deterministic family classification.
4. Reconcile the ledger and record exact pre-change counts.
5. Reproduce each selected finding sufficiently to assign priority and disposition.
6. Inspect implementations, callers, public contracts, and tests before editing.
7. Make only the approved changes for the batch.
8. Run focused tests and the target project build.
9. Run sequential solution build and tests when the risk or blast radius warrants them.
10. Generate a fresh post-change InspectCode report.
11. Reconcile added, removed, changed, and persistent fingerprints.
12. Confirm that no new higher-priority findings were introduced.
13. Record validation evidence and review the complete diff.

* **BAT-001**: Every batch must define measurable entry and exit counts before editing.
* **BAT-002**: Every batch must regenerate the baseline after its changes.
* **BAT-003**: Every resolved fingerprint must identify its disposition and validation evidence.
* **BAT-004**: A batch is incomplete when post-change SARIF or ledger counts do not reconcile.
* **BAT-005**: A batch that introduces a new higher-priority finding must fix it or stop for explicit risk acceptance.
* **BAT-006**: A batch must not claim success solely because total findings decreased.

## Validation Gates

### Focused behavior verification

Behavior-changing fixes require focused unit or integration tests for the affected path. Tests must include the reported edge condition where practical.

### Project build

Every source-changing batch must build each affected project. Compiler warnings and errors introduced by the batch are prohibited.

### Solution verification

Run the full solution build and relevant solution tests sequentially when a batch changes shared libraries, public contracts, cross-project behavior, concurrency, disposal, persistence, serialization, or broad rule configuration.

### Static-analysis verification

Every batch must produce a fresh InspectCode SARIF report with the same solution target and effective settings as its pre-change report. The review must record:

* total before and after
* count delta by severity
* count delta by root
* count delta by family
* selected fingerprint outcomes
* newly introduced fingerprints
* unresolved higher-priority findings in the batch scope
* standard-error tool noise kept outside SARIF counts

### Roslyn style verification

Roslyn style verification must remain clean or any change must be explained and separately approved. The prior command analyzed 4,710 files and produced 0 diagnostics.

* **VAL-001**: Focused tests must pass for every behavior-changing batch.
* **VAL-002**: Affected project builds must pass with no new compiler warning or error.
* **VAL-003**: Solution build and tests must run sequentially when required by risk.
* **VAL-004**: Post-change InspectCode output must be freshly generated and exactly reconciled with the ledger.
* **VAL-005**: No batch may introduce an unresolved finding of higher priority than its selected findings.
* **VAL-006**: Count reduction without fingerprint reconciliation is invalid evidence.

## Reproducibility

The commands below are PowerShell examples. They assume execution from the repository root. A temporary `jb.exe` installation is an example only and must not be committed, referenced by project files, or treated as a repository dependency.

### InspectCode baseline

```powershell
$toolRoot = Join-Path $env:TEMP 'bitdevkit-jetbrains-tools'
$toolPath = Join-Path $toolRoot 'jb.exe'
$reportPath = Join-Path $env:TEMP 'bitdevkit-inspectcode-current.sarif'

dotnet tool install JetBrains.ReSharper.GlobalTools `
    --tool-path $toolRoot

& $toolPath inspectcode .\bITdevKit.slnx `
    --no-build `
    --swea `
    --severity=WARNING `
    --output=$reportPath `
    --format=Sarif `
    --no-updates `
    --verbosity=WARN

if ($LASTEXITCODE -ne 0) {
    throw "InspectCode failed with exit code $LASTEXITCODE."
}
```

If the temporary tool already exists, use `dotnet tool update` instead of reinstalling it. The command's standard error must be retained separately from SARIF so generated Razor loading noise is not counted as a source finding.

### Compiler build

```powershell
dotnet build .\bITdevKit.slnx --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Solution build failed with exit code $LASTEXITCODE."
}
```

### Roslyn style verification

```powershell
dotnet format .\bITdevKit.slnx style `
    --verify-no-changes `
    --no-restore `
    --verbosity diagnostic

if ($LASTEXITCODE -ne 0) {
    throw "Roslyn style verification failed with exit code $LASTEXITCODE."
}
```

### Sequential tests

```powershell
dotnet test .\bITdevKit.slnx --nologo --no-build

if ($LASTEXITCODE -ne 0) {
    throw "Solution tests failed with exit code $LASTEXITCODE."
}
```

InspectCode command options may be adjusted for an installed tool version, but pre-change and post-change reports for a batch must use equivalent targets and effective settings. The exact command and tool version must be stored in validation evidence.

## Batch Acceptance Criteria

Every batch is complete only when all applicable conditions are true:

* **BAC-001**: The batch declares its selected files, rules, fingerprints, and pre-change counts.
* **BAC-002**: Every selected finding has a family, priority, disposition, rationale, owner or batch, and status.
* **BAC-003**: The batch changes only its declared cohesive scope.
* **BAC-004**: Public compatibility review is recorded for every public or protected API impact.
* **BAC-005**: Focused tests pass for behavior-changing fixes.
* **BAC-006**: Every affected project builds successfully with no new compiler warnings or errors.
* **BAC-007**: Sequential solution build and tests pass when required by the validation gates.
* **BAC-008**: A fresh post-change InspectCode report is generated with equivalent settings.
* **BAC-009**: The post-change ledger and SARIF totals reconcile exactly by total, severity, root, and family.
* **BAC-010**: Every selected fingerprint is resolved, intentionally retained with rationale, or explicitly deferred.
* **BAC-011**: No unresolved higher-priority finding is introduced.
* **BAC-012**: The batch records exact count deltas and validation commands.

## Campaign Acceptance Criteria

The campaign is complete when all conditions below are true:

* **AC-001**: Every current in-scope SARIF finding has exactly one stable ledger entry or documented successor fingerprint.
* **AC-002**: Every ledger entry has a deterministic family, priority, disposition, rationale, owner or batch, status, first and last seen values, and validation evidence.
* **AC-003**: Generated and external code is excluded before classification, and exclusion evidence is retained.
* **AC-004**: Family totals reconcile exactly to the filtered report total under the documented precedence.
* **AC-005**: Root totals and analyzer severity totals reconcile exactly to the filtered report total.
* **AC-006**: All P0 findings are resolved or have explicit approved risk acceptance supported by reproduction evidence.
* **AC-007**: All P1 findings are resolved, suppressed with rationale, identified as tool limitations, or deferred with an owner and review trigger.
* **AC-008**: Compiler/model-loading findings are reconciled against compiler or editor evidence rather than promoted automatically from analyzer severity.
* **AC-009**: No generated files, migrations, designers, source-generated Razor files, generated API clients, or build outputs are modified by the campaign.
* **AC-010**: No bulk public rename or removal occurs, and every approved public API change has semantic compatibility review.
* **AC-011**: Every completed batch has a regenerated baseline and exact before-and-after fingerprint reconciliation.
* **AC-012**: The final solution build succeeds with 0 warnings and 0 errors.
* **AC-013**: Required focused, project, and sequential solution tests pass.
* **AC-014**: The final InspectCode report introduces no unresolved P0 or P1 finding relative to the preceding accepted baseline.
* **AC-015**: The four custom naming rules remain at zero findings.
* **AC-016**: Generated Razor loading warnings emitted on standard error remain documented as tool noise outside SARIF totals.
* **AC-017**: Every remaining finding has an intentional, reviewable disposition; campaign completion does not require a zero-finding report.

## Risks and Mitigations

### Risk: analyzer errors are mistaken for compiler failures

Mitigation: reproduce compiler/model-loading findings with `dotnet build` or the supported editor. Preserve the distinction between analyzer severity and compiler outcome.

### Risk: broad quick-fixes change behavior

Mitigation: use small batches, inspect complete diffs, test behavior, and prohibit unreviewed bulk application.

### Risk: public compatibility is broken during cleanup

Mitigation: prohibit bulk public renames and removals; require semantic compatibility review for every public or protected API change.

### Risk: dead-code analysis misses dynamic use

Mitigation: inspect reflection, serialization, dependency injection, source generation, tests, examples, and external API exposure before deletion.

### Risk: suppressions hide valid defects

Mitigation: require narrow scope, durable rationale, and reviewable evidence for every suppression.

### Risk: generated code enters the work queue

Mitigation: apply generated-path and semantic exclusions before family classification and retain exclusion evidence.

### Risk: baseline counts become stale

Mitigation: regenerate InspectCode output and ledger aggregates for every batch.

### Risk: count reduction becomes the objective

Mitigation: measure fingerprint outcomes, priority, validation evidence, and new higher-priority findings rather than only total count.

### Risk: InspectCode model noise obscures source defects

Mitigation: keep standard-error loading warnings separate, use `Needs-tooling`, and reconcile suspicious findings with compiler and editor evidence.

### Risk: concurrent builds produce misleading failures

Mitigation: run top-level solution build and test operations sequentially in one worktree.

## Open Decisions

* **DEC-001**: Select the ledger storage format and ownership model.
* **DEC-002**: Define the exact normalized fingerprint algorithm and migration behavior when it changes.
* **DEC-003**: Select the checked-in mechanism, if any, for deterministic SARIF filtering and family mapping.
* **DEC-004**: Define maximum batch size by files, findings, or review effort.
* **DEC-005**: Decide which advisory rules should remain enabled after representative manual review.
* **DEC-006**: Define the approval authority and review interval for deferred P0 and P1 findings.
* **DEC-007**: Decide whether temporary tool version metadata is stored in the ledger, batch report, or continuous-integration artifact.
* **DEC-008**: Determine whether InspectCode becomes a continuous-integration gate after baseline triage and, if so, which priority or new-finding threshold it enforces.

## Validation Criteria

The specification itself is internally valid only when these equations hold:

```text
354 errors + 8,696 warnings = 9,050 findings

6,788 src + 1,819 tests + 426 examples + 17 benchmarks = 9,050 findings

2,449 dead or unused
+ 1,740 documentation
+ 1,705 naming/spelling/style
+ 1,236 correctness/nullability/control-flow
+   729 API/design/encapsulation
+   696 performance/allocation
+   282 compiler/model-loading
+   127 other/manual classification
+    86 disposal/async/concurrency
= 9,050 findings
```

The severity and root subtotals in each family table must also equal that family's total. A future baseline may change the numbers, but it must satisfy the same reconciliation requirements.

## Related Files

* `docs/specs/spec-public-api-xml-documentation-coverage.md`
* `AGENTS.md`
* `.editorconfig`
* `Directory.Build.props`
* `bITdevKit.slnx`

## Summary

This campaign turns 9,050 broad InspectCode findings into an evidence-based triage ledger and a sequence of small, reviewable batches. It excludes generated code first, assigns one deterministic family per finding, protects public compatibility, distinguishes analyzer severity from compiler failure, and requires a fresh reconciled baseline after every batch. Completion means that every remaining finding has an intentional validated disposition, not that every analyzer suggestion has been bulk-fixed.
