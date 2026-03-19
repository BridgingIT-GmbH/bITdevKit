# Skill Creation Workflow Checklist

Use this checklist to guide your end-to-end Agent Skill creation process. Follow steps sequentially and mark each item complete before moving to the next phase.

## Phase 1: Planning & Design

### Step 1: Define Scope

- [ ] **Identify the task**: What specific problem does this skill solve?
- [ ] **Determine skill type**: Workflow (multi-step) or Tool (CLI wrapper) or Analysis (validation)?
- [ ] **Assess complexity**: Simple (< 500 lines) or Complex (500+ lines requiring subdirectories)?
- [ ] **Verify it's a skill**: Confirm this should be a skill, not a custom instruction (see `skill-vs-instructions-matrix.md`)

### Step 2: Research & Gather Context

- [ ] **Study similar skills**: Review existing skills (adr-writer, nuget-manager) for patterns
- [ ] **Identify source material**: What docs, code, or examples will inform this skill?
- [ ] **List required tools**: What CLIs, frameworks, or libraries does the skill use?
- [ ] **Define prerequisites**: What knowledge or setup must user have before using skill?

### Step 3: Design Structure

- [ ] **Draft frontmatter**: Write `skill`, `description`, `globs`, `alwaysApply` fields
- [ ] **Outline workflow**: Break task into sequential steps (aim for 5-15 major steps)
- [ ] **Identify templates needed**: Will skill need code templates or file structures?
- [ ] **Plan subdirectories**: If > 5 templates or complex reference material, plan subdirectory structure
- [ ] **Determine validation strategy**: What checkpoints ensure quality?

### Step 4: Validate Planning

- [ ] **Pass specificity test**: Description states WHAT, HOW, and WHEN clearly
- [ ] **Pass portability test**: Skill works across similar projects (not overly specific to one codebase)
- [ ] **Pass completeness test**: Workflow covers end-to-end task (not just partial steps)
- [ ] **Get feedback**: Show plan to colleague or user for sanity check

## Phase 2: Create Skill Structure

### Step 5: Create Directory & SKILL.md

- [ ] **Create skill directory**: `.github/skills/[skill-name]/`
- [ ] **Create SKILL.md**: Start with frontmatter (use `templates/basic-skill-template.md` or `advanced-skill-template.md`)
- [ ] **Write Overview section**: 2-3 sentence summary of skill purpose
- [ ] **Write Prerequisites section**: List required tools, knowledge, or setup
- [ ] **Write Core Rules section**: 3-7 critical rules (NEVER/ALWAYS statements)

### Step 6: Document Workflow

- [ ] **Break workflow into phases**: Group related steps (e.g., Planning, Execution, Validation)
- [ ] **Write step-by-step procedures**: Each step should be atomic and actionable
- [ ] **Add code examples**: Show actual commands, not placeholders
- [ ] **Add expected outputs**: Help users confirm success
- [ ] **Include decision points**: Where do users need to make choices?

### Step 7: Create Templates (if needed)

- [ ] **Create `templates/` directory**: Only if 3+ templates needed
- [ ] **Write template files**: Use realistic code, not pseudocode
- [ ] **Add inline comments**: Explain "why" and "pattern", not just "what"
- [ ] **Use placeholder markers**: Consistent format like `[Entity]`, `[Module]`, `[Property]`
- [ ] **Test templates**: Verify they compile/run in real environment

### Step 8: Add Examples

- [ ] **Create 2+ realistic scenarios**: One simple, one complex
- [ ] **Use actual user language**: "Add X to Y" not "Execute operation Z"
- [ ] **Show exact agent actions**: Commands, filenames, step references
- [ ] **Map to workflow steps**: Examples should reference workflow clearly
- [ ] **Consider subdirectory**: If examples > 50 lines each, move to `examples/`

## Phase 3: Add Reference Material

### Step 9: Create Quality Checklist

- [ ] **List verification items**: Checklist items should be binary (yes/no)
- [ ] **Cover all workflow steps**: Every major step should have corresponding checklist items
- [ ] **Make criteria specific**: "5+ reasons" not "sufficient rationale"
- [ ] **Order by importance**: Critical items first
- [ ] **Consider subdirectory**: If checklist > 30 items, split into multiple files in `checklists/`

### Step 10: Document Common Pitfalls

- [ ] **Identify anti-patterns**: What mistakes do users commonly make?
- [ ] **Show WRONG examples**: Actual bad code/commands, not abstract descriptions
- [ ] **Provide CORRECT alternatives**: Parallel structure (same number of items in WRONG and CORRECT sections)
- [ ] **Explain why**: Briefly state why the anti-pattern fails
- [ ] **Use plain ASCII**: Write "WRONG" and "CORRECT", not emoji

### Step 11: Add Supporting Documentation (if needed)

- [ ] **Create `docs/` directory**: Only if substantial reference material (> 200 lines total)
- [ ] **Architecture overview**: If skill spans multiple layers or systems
- [ ] **Naming conventions**: If skill creates many files with specific naming
- [ ] **Troubleshooting guide**: Common errors and solutions
- [ ] **References**: Links to external docs, ADRs, or project documentation

## Phase 4: Quality Assurance

### Step 12: Validate Frontmatter

- [ ] **Frontmatter syntax**: Valid YAML (no indentation errors)
- [ ] **skill**: Matches directory name, uses kebab-case
- [ ] **description**: 60-120 characters, starts with action verb, no trailing period, single line
- [ ] **globs**: Appropriate file patterns (or empty array `[]`)
- [ ] **alwaysApply**: Set to `true` or `false` (justified if `true`)

### Step 13: Validate Content Quality

- [ ] **Core Rules**: 3-7 rules, all enforceable, use NEVER/ALWAYS
- [ ] **Workflow**: Steps are sequential, atomic, and actionable
- [ ] **Examples**: At least 2 scenarios, realistic user language
- [ ] **Code samples**: Actual commands/code, not placeholders
- [ ] **Consistency**: Terminology is consistent throughout
- [ ] **Grammar/spelling**: No typos or grammatical errors

### Step 14: Test Progressive Disclosure

- [ ] **Level 1 (Quick Start)**: Can user start in < 30 seconds?
- [ ] **Level 2 (Workflow)**: Can user follow without getting lost?
- [ ] **Level 3 (Reference)**: Can user find answers to edge cases?
- [ ] **No duplication**: Same info isn't repeated unnecessarily
- [ ] **Lazy loading**: Subdirectory files are loaded on-demand, not required upfront

### Step 15: Validate Against Standards

- [ ] **agentskills.io compliant**: Frontmatter uses `skill`, `description`, `globs`, `alwaysApply`
- [ ] **Follows project conventions**: Matches style of existing skills (adr-writer, nuget-manager)
- [ ] **Self-contained**: Doesn't reference chatmodes or internal dev docs
- [ ] **Portable**: Works across similar projects, not overly specific
- [ ] **Accessible**: Uses plain ASCII (no emoji or special Unicode)

## Phase 5: Testing & Refinement

### Step 16: Functional Testing

- [ ] **Test with AI agent**: Have agent execute skill with realistic user request
- [ ] **Verify outputs**: Agent produces correct files/changes
- [ ] **Check quality**: Outputs follow conventions and best practices
- [ ] **Measure time**: Workflow completes in reasonable timeframe
- [ ] **Test edge cases**: Skill handles variations (different modules, missing files, etc.)

### Step 17: Usability Testing

- [ ] **Test with human user**: Have colleague follow skill manually
- [ ] **Gather feedback**: Are steps clear? Any confusion?
- [ ] **Identify gaps**: What information is missing?
- [ ] **Refine based on feedback**: Update skill to address issues
- [ ] **Retest**: Verify changes resolve problems

### Step 18: Documentation Testing

- [ ] **Verify all links**: Internal references and external URLs work
- [ ] **Check file paths**: Referenced files/directories exist
- [ ] **Test code samples**: Commands/code compile and run
- [ ] **Validate checksums**: No broken images or corrupted files
- [ ] **Review formatting**: Markdown renders correctly

## Phase 6: Publication & Maintenance

### Step 19: Prepare for Publication

- [ ] **Final review**: Read entire skill start to finish
- [ ] **Spell check**: No typos
- [ ] **Format check**: Consistent heading levels, code blocks, lists
- [ ] **File organization**: All files in correct directories
- [ ] **Update project docs**: Add skill to `AGENTS.md` or similar
- [ ] **Create commit**: Meaningful commit message describing new skill

### Step 20: Post-Publication

- [ ] **Monitor usage**: Track how often skill is used
- [ ] **Collect feedback**: Gather user reports of issues
- [ ] **Track maintenance needs**: Note where skill needs updates
- [ ] **Version control**: Consider versioning if skill changes significantly
- [ ] **Deprecation plan**: If superseded, document migration path

## Troubleshooting Checklist Failures

### If Skill Feels Incomplete
→ Revisit Phase 1: Ensure workflow covers end-to-end task

### If Steps Are Confusing
→ Revisit Phase 2 Step 6: Make steps more atomic, add examples

### If Skill Is Too Long (> 1000 lines)
→ Revisit Phase 2 Step 7: Extract templates to subdirectory
→ Revisit Phase 3 Step 11: Extract reference material to `docs/`

### If Skill Is Too Vague
→ Revisit Phase 1 Step 3: Add specificity (file paths, commands, examples)
→ Revisit Phase 2 Step 6: Replace abstract descriptions with concrete commands

### If Skill Isn't Used
→ Revisit Phase 1 Step 4: Validate this should be a skill (not custom instruction)
→ Revisit Phase 4 Step 12: Check `globs` patterns trigger correctly

## Time Estimates

| Phase | Estimated Time | Notes |
|-------|---------------|-------|
| Phase 1: Planning | 30-60 minutes | Critical phase, don't rush |
| Phase 2: Structure | 1-3 hours | Depends on workflow complexity |
| Phase 3: Reference | 1-2 hours | Can be shorter if simple skill |
| Phase 4: QA | 30-60 minutes | Worth the investment |
| Phase 5: Testing | 1-2 hours | Catch issues before publication |
| Phase 6: Publication | 15-30 minutes | Wrap-up and docs |
| **Total** | **4-9 hours** | Simple skill: 4-5 hours, Complex: 7-9 hours |

## Success Criteria

Before marking skill complete, verify:

- [ ] **Functional**: Agent successfully executes skill end-to-end
- [ ] **Complete**: All workflow steps are documented
- [ ] **Quality**: Outputs meet project standards
- [ ] **Clear**: User can follow without external help
- [ ] **Efficient**: Workflow completes in reasonable time
- [ ] **Maintainable**: Future updates are straightforward
- [ ] **Standard-Compliant**: Follows agentskills.io and project conventions

## References

- Example Skills: `.github/skills/adr-writer/SKILL.md`, `.github/skills/nuget-manager/SKILL.md`
- Templates: `.github/skills/skill-creator/templates/`
- Examples: `.github/skills/skill-creator/examples/`
- Standards: [agentskills.io](https://agentskills.io), [VS Code Docs](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
