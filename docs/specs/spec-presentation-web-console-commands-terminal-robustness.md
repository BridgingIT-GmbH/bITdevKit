---
created: 2026-08-05
status: draft
---

# Design Specification: Cross-Platform Interactive Console Robustness

> Restore reliable Console Commands on Linux immediately with the former
> `Console.ReadLine()` behavior, then redesign enhanced prompt rendering so
> concurrent logs and terminal input cannot block the application process.

[TOC]

Related documents:

* [Web Console for Console Commands](spec-web-console-commands.md)
* [DevKit Application Host](spec-presentation-devkit-console-host.md)
* [Console Commands feature documentation](../features-presentation-console-commands.md)

## Overview

The terminal frontend for Console Commands provides an enhanced command-line
editor with history navigation, cursor movement, in-line editing, and a prompt
that remains at the bottom while application logs are written above it.

The enhanced behavior works on Windows but can block a Linux application while
it waits for command input. When blocked, pressing Enter allows pending work and
logs to advance until the input loop blocks again. After application logging
becomes idle, the apparent process block is no longer visible.

The issue is caused by combining:

* a blocking `Console.ReadKey()` input operation;
* `Console.CursorLeft` and `Console.CursorTop` reads used by
  `InteractiveConsoleCoordinator`;
* synchronous Serilog writes through the same coordinator; and
* one process-wide coordinator lock.

On Linux, reading the console cursor position can write a terminal cursor
position request and read its response from standard input. The .NET Unix
console implementation serializes that operation with `Console.Read*`.
Consequently, a log redraw that reads the cursor position waits for the active
`Console.ReadKey()` operation. The log sink remains synchronous and holds the
coordinator lock while it waits, allowing the blockage to propagate into
request processing, background services, and graceful shutdown.

This specification separates remediation into two delivery phases:

1. **Immediate Linux fallback:** restore the former `Console.ReadLine()` input
   behavior and do not activate coordinated bottom-line rendering on Linux.
2. **Robust enhanced terminal:** redesign coordinated input and output so
   enhanced editing can later be enabled on Linux without terminal reads from
   logging threads or process-wide blocking.

## Observed Behavior

The defect has the following externally observable characteristics:

* Windows terminal input and prompt-preserving log rendering continue to work.
* Linux reaches the interactive prompt and then application progress associated
  with synchronous logging stops.
* Pressing Enter releases enough input work for queued logs or application work
  to advance, after which the process blocks again.
* Repeated Enter presses can repeatedly advance the process to the next block.
* When no further application logs are emitted, the blockage is no longer
  apparent.
* Terminal output may contain the ANSI cursor position request `ESC[6n` when
  the terminal does not answer it.
* A graceful stop can be delayed when shutdown logging enters the blocked
  coordinator path.
* Redirected input or output already uses `Console.ReadLine()` and does not
  activate the enhanced prompt.

The phrase "process is blocked" in this specification means application threads
that synchronously emit console logs can block behind console input. The web
server may initially remain reachable, but continued synchronous logging can
stall progressively more application work.

## Failure Flow

```text
Linux terminal input thread
  -> Console.ReadKey()
  -> owns/waits inside the .NET Unix standard-input reader

Application or background thread
  -> synchronous Serilog ConsoleSink.Emit()
  -> InteractiveConsoleCoordinator.Write()
  -> owns the coordinator lock
  -> redraws the prompt
  -> reads Console.CursorLeft / Console.CursorTop
  -> waits for the .NET Unix standard-input reader

Enter key
  -> Console.ReadKey() returns temporarily
  -> one or more waiting operations advance
  -> input loop calls Console.ReadKey() again
  -> blocking repeats
```

Covers acceptance criteria: QF-1, QF-2, RT-1, RT-2.

## Goals

* Prevent the interactive Console Commands feature from blocking Linux
  application work.
* Deliver a small, low-risk Linux fallback before redesigning the enhanced
  terminal.
* Preserve Console Commands registration, parsing, execution, and output on
  Linux.
* Keep current enhanced command-line editing on Windows.
* Preserve existing redirected-input and redirected-output behavior.
* Make graceful application shutdown complete while command input is idle.
* Define a portable enhanced terminal design that never reads stdin from a log
  writer.
* Add regression coverage using a Linux pseudo-terminal.

## Non-Goals

This specification does not:

* change command discovery, binding, validation, authorization, or execution;
* change the browser-based Console Commands frontend;
* change host-command forwarding over local IPC;
* replace Spectre.Console command output;
* provide enhanced Linux history navigation in the immediate quick fix;
* preserve the prompt at the bottom on Linux in the immediate quick fix;
* guarantee clean prompt rendering when Linux logs arrive while a user is
  typing in the immediate quick fix;
* make interactive Console Commands available outside the existing local
  development and Kestrel eligibility rules;
* require the long-term implementation to use a particular terminal library.

## Delivery Order

The phases are intentionally independent:

```text
Phase 1: Linux safe fallback
  -> may be implemented and released immediately
  -> restores old, reliable behavior

Phase 2: Robust enhanced terminal
  -> implemented and validated separately
  -> replaces the Linux fallback only after PTY regression tests pass
```

Phase 1 must not wait for Phase 2 design or implementation.

## Story 1: Immediate Linux Safe Fallback

* Status: Implemented
* Priority: Immediate
* Ready: Yes
* Ready Reason: The affected platform, fallback behavior, compatibility
  boundary, and observable acceptance criteria are defined.
* User Story: As a developer running a DevKit web application on Linux, I want
  Console Commands to use reliable line-based input, so that the application
  continues processing logs and work while it waits for a command.

### Required Behavior

When both input and output are attached to a terminal, the terminal input mode
shall be selected as follows:

```text
Linux
  -> write the ordinary prompt
  -> call Console.ReadLine()
  -> do not begin an InteractiveConsoleCoordinator input session

Windows
  -> retain the current enhanced Console.ReadKey() editor
  -> retain coordinated prompt-preserving log rendering

Redirected input or output on any platform
  -> retain the existing Console.ReadLine() behavior
```

The Linux path should be equivalent to the earlier terminal loop:

```csharp
console.Markup("[grey]> [/]");
var line = Console.ReadLine();
```

The example is behavioral, not a required method shape.

### Acceptance Criteria

1. **QF-1:** Given WeatherFiesta is running in Development on Linux with an
   interactive terminal, when the prompt is waiting and background services
   emit logs, then those services and log calls continue without requiring a
   keypress.
2. **QF-2:** Given the Linux prompt is waiting, when the user presses Enter with
   an empty line, then the loop displays the next prompt normally rather than
   releasing queued process work.
3. **QF-3:** Given Linux uses terminal input, when a command is entered, then it
   is parsed and executed by the existing `ConsoleCommandExecutor` with the
   same output and command history recording semantics as before.
4. **QF-4:** Given Linux uses terminal input, when command entry starts or logs
   are written, then no `InteractiveConsoleCoordinator` input session is
   active and the Linux command-entry path does not read `Console.CursorLeft`
   or `Console.CursorTop`.
5. **QF-5:** Given Windows uses terminal input, when command entry starts, then
   the current enhanced editing, history navigation, and prompt-preserving log
   rendering remain enabled.
6. **QF-6:** Given input or output is redirected on any supported platform,
   when the loop reads a command or reaches end-of-input, then existing
   `Console.ReadLine()` and EOF behavior remain unchanged.
7. **QF-7:** Given a Linux host is waiting in `Console.ReadLine()`, when
   application shutdown is requested, then prompt coordination and console
   logging do not prevent the host from completing graceful shutdown.

### Compatibility Notes

The Linux quick fix deliberately accepts the old user experience:

* logs may appear after the prompt or interleave visually with typed input;
* Up/Down history navigation is unavailable;
* Left/Right, Home/End, Delete, and word movement are handled only according to
  the terminal's normal line discipline;
* the prompt is not continuously restored at the bottom;
* commands and basic command history persistence remain available.

The `ConsoleSink` may continue to use `InteractiveConsoleCoordinator` as its
shared writer. Because the Linux input path never calls `BeginInput`, the
coordinator remains inactive and performs an ordinary serialized write without
prompt clearing, cursor-position reads, or prompt redraw.

## Story 2: Robust Enhanced Terminal

* Status: Pending
* Priority: Follow-up
* Ready: Yes
* Ready Reason: Required concurrency, lifecycle, fallback, and user-facing
  outcomes are defined; the implementation mechanism remains negotiable.
* User Story: As a developer using Console Commands on a supported interactive
  terminal, I want enhanced command editing and prompt-preserving logs without
  blocking application work, so that the terminal remains useful during active
  application logging.

### Design Constraints

The robust implementation must preserve the following invariant:

> A thread writing an application log must never read from standard input or
> wait for an active console read to finish.

The implementation shall:

* avoid `Console.CursorLeft` and `Console.CursorTop` reads while interactive
  input is active;
* avoid any terminal cursor-position query/response protocol from synchronous
  log calls;
* maintain prompt buffer and cursor position as application state;
* use output-only, relative terminal operations for clearing and redrawing a
  prompt, or use an equivalent terminal abstraction with the same
  non-blocking property;
* serialize terminal writes so log lines and prompt redraws are not corrupted
  by concurrent output;
* ensure the input operation can observe application shutdown or cannot hold up
  host shutdown;
* detect unsupported terminal capabilities and fall back to basic
  `Console.ReadLine()` behavior;
* keep redirected input and output on the basic path;
* keep command execution independent from the selected terminal editor.

A likely rendering model is:

```text
terminal renderer owns logical prompt state
  -> clear current line using output-only relative ANSI
  -> write complete log line
  -> write prompt and current buffer
  -> move cursor relative to the tracked logical buffer position
```

This is a design direction, not a mandate to hand-code ANSI sequences. A
terminal library or dedicated renderer is acceptable if it obeys the invariant.

### Acceptance Criteria

1. **RT-1:** Given enhanced input is waiting for a key, when another thread
   emits a console log, then the log call and associated application work
   complete without a user keypress.
2. **RT-2:** Given enhanced input and continuous background logging are active,
   when no key is pressed for at least 30 seconds, then logs continue to render
   and application work continues without thread accumulation caused by
   terminal coordination.
3. **RT-3:** Given a supported terminal, when logs arrive while the user has a
   partially typed command, then the prompt, buffer, and logical cursor are
   restored without losing or duplicating characters.
4. **RT-4:** Given enhanced input is waiting, when application shutdown is
   requested, then the terminal loop exits or becomes irrelevant to process
   lifetime and the host completes graceful shutdown without a keypress.
5. **RT-5:** Given terminal capabilities are missing, unreliable, or
   redirected, when the terminal frontend starts, then it selects basic
   line-based input without emitting raw cursor-position requests.
6. **RT-6:** Given Windows enhanced behavior is supported, when the robust
   renderer is introduced, then existing editing keys, command history, prompt
   styling, and coordinated logging remain functionally equivalent.
7. **RT-7:** Given a command produces multi-line Spectre.Console output, when it
   completes, then the output is intact and the next prompt is rendered once.

## Story 3: Regression Coverage

* Status: Partial
* Priority: Required with each phase
* Ready: Yes
* Ready Reason: The test environments, triggering events, and expected
  liveness outcomes are explicit.
* User Story: As a DevKit maintainer, I want automated terminal concurrency
  regression tests, so that Linux blocking behavior cannot be reintroduced.

### Acceptance Criteria

1. **TEST-1:** A Linux pseudo-terminal test starts a minimal host, waits for the
   prompt, emits a background log, and verifies completion without sending
   input.
2. **TEST-2:** A Linux quick-fix test verifies the selected input mode is basic,
   no coordinator input session becomes active, and command execution still
   succeeds.
3. **TEST-3:** A Windows test or platform-neutral selector test verifies that
   Windows continues selecting enhanced input.
4. **TEST-4:** A redirected-input test verifies EOF stops the input loop without
   repeated prompts or exceptions.
5. **TEST-5:** A shutdown test requests host termination while input is idle and
   verifies bounded graceful completion without sending Enter.
6. **TEST-6:** Phase 2 adds a stress test with continuous logs and idle enhanced
   input for at least 30 seconds, followed by command entry and graceful
   shutdown.

Tests must use bounded timeouts and must fail with a diagnostic indicating
whether input, logging, command execution, or shutdown exceeded the bound.

## Implementation Scope

Expected Phase 1 touchpoints:

```text
src/Presentation.Web/ConsoleCommands/ApplicationBuilderExtensions.cs
tests/Presentation.UnitTests/ConsoleCommands/
tests/Presentation.IntegrationTests/ or an existing PTY-capable test location
```

Expected Phase 2 touchpoints:

```text
src/Presentation/ConsoleCommands/InteractiveConsoleCoordinator.cs
src/Presentation.Web/ConsoleCommands/ApplicationBuilderExtensions.cs
src/Presentation.Serilog/ConsoleSink.cs
tests/Presentation.UnitTests/ConsoleCommands/
PTY-capable integration tests
```

The exact test project may follow the repository's existing project boundaries.
Application code in `Presentation.Web` must not introduce infrastructure or
application-layer dependencies.

## Diagnostics and Failure Handling

* Expected basic-mode selection must not be logged as an error or warning.
* Terminal capability fallback may be logged once at Debug level.
* Console input exceptions must not create a rapid retry loop.
* EOF must stop the interactive input loop.
* A terminal rendering failure in the future enhanced implementation must
  disable enhanced rendering or end the terminal frontend without blocking the
  web host.
* Logging failures must not be recursively logged through the same failing
  console sink.

## Definition of Ready

### Phase 1

* Status: Implemented
* Ready: Yes
* Platform boundary: Linux only for the new fallback; Windows remains enhanced.
* Dependencies: Existing `Console.ReadLine()` path and command executor.
* Data changes: None.
* External input: None.
* Known trade-off: Linux temporarily loses enhanced editing and bottom-line
  prompt preservation.

### Phase 2

* Ready: Yes
* Platform boundary: All supported interactive terminals.
* Dependencies: Terminal capability detection and a PTY-capable test harness.
* Data changes: None.
* External input: None required before implementation; the renderer or library
  choice remains an implementation decision.
* Known risk: ANSI capability differences and terminal resizing must not
  reintroduce stdin queries into the logging path.

## Open Questions

* Should the robust renderer remain internal to Console Commands or become a
  reusable Presentation terminal abstraction?
* Should Phase 2 use output-only ANSI rendering directly or adopt a terminal
  editor library after a focused compatibility evaluation?
* Should macOS retain the current enhanced editor until it has its own PTY
  coverage, or proactively use the Linux-style safe fallback?

None of these questions blocks the Linux Phase 1 quick fix.
