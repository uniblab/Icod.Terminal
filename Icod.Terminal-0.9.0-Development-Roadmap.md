# Icod.Terminal 0.9.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.9.0`  
**Current version:** `0.9.0`  
**Predecessor:** `0.8.0` — typed cursor-style control and truthful restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** synchronized output and nested transactional output state  
**Status:** T90–T97 complete; merge requires exact-head green validation

---

## 1. Release objective

`Icod.Terminal 0.9.0` completes the 0.4–0.9 operational protocol-closure sequence by adding semantic synchronized-output ownership on top of the session output gate, lifecycle machinery, active query substrate, and scoped-state mechanisms established in earlier releases.

The protocol target is DEC private mode 2026:

```text
CSI ? 2026 h
CSI ? 2026 l
```

Public callers work with semantic synchronized-output ownership rather than raw DECSET/DECRST construction.

Synchronized output remains opt-in. Ordinary terminal writes do not silently enable it.

---

## 2. Existing foundations reused by 0.9

0.9 builds on:

- session-owned serialized semantic output;
- complete-frame commit semantics;
- canonical seven-bit CSI output;
- reversible presentation leases;
- hyperlink and cursor-style ownership patterns;
- lifecycle prepare/suspend/resume participation;
- deterministic session disposal;
- one shared input reader and query transaction manager;
- typed DECRQSS support;
- truthful state restoration policy;
- package-only consumer validation across net8.0/net9.0/net10.0.

No second output transport, reader loop, or raw public CSI surface is introduced.

The historical advanced `WriteTerminalStringAsync(...)` surface remains a deliberate low-level exception: session semantic managers hold the shared control-output gate around their terminfo transition batches, while direct external callers remain responsible for coordinating that raw output with concurrent session traffic.

---

## 3. Completed tranche sequence

### T90 — synchronized-output contract and reference freeze — complete

Frozen in `docs/T90-Synchronized-Output-Contract-and-Reference-Freeze.md`:

- canonical seven-bit DEC private mode 2026 begin/end bytes;
- first-owner/last-owner physical transitions;
- identity-aware logical ownership with out-of-order disposal allowed;
- no repeated nested begin frames;
- no implicit flush after begin;
- final physical leave emits mode-off and flushes once;
- cancellation-before-commit semantics;
- failure-retained cleanup ownership;
- suspend/resume/disposal behavior;
- composition with text, presentation, OSC, cursor style, and active queries;
- optimistic support posture with no TERM/OS/emulator inference;
- no automatic DECRQM probe;
- terminal-side timeout/implementation limits acknowledged explicitly;
- no public generic CSI/DECSET/DECRST surface.

### T91 — internal synchronized-output CSI primitive — complete

Implemented:

- internal canonical `CSI ? 2026 h` begin frame;
- internal canonical `CSI ? 2026 l` end frame;
- specialized `CsiWriter` helpers built on the existing structural CSI encoder;
- complete-frame construction before commit;
- cancellation-before-commit behavior;
- byte-exact tests;
- no generic private-mode API.

Workflow #313 was green.

Record: `docs/T91-Synchronized-Output-CSI-Primitive.md`.

### T92 — synchronized-output state manager and nesting — complete

Implemented:

- session-owned `TerminalSynchronizedOutputManager`;
- logical owner IDs independent from physical mode state;
- first-owner enter / last-owner leave semantics;
- arbitrary non-final release order;
- failure-retained ownership;
- session-disposal cleanup;
- lifecycle participant integration;
- no duplicate physical mode toggles for nested logical owners;
- lock ordering: manager gate followed by short-lived session output gate only during a physical transition.

Workflow #320 was green.

Record: `docs/T92-Synchronized-Output-State-Manager-and-Nesting.md`.

### T93 — public synchronized-output lease/API — complete

Stable public API:

```csharp
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable {
	public ValueTask DisposeAsync();
}

public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
	CancellationToken cancellationToken = default
);
```

Frozen behavior:

- logical ownership rather than proof of terminal support;
- nested public leases share one physical begin/end pair;
- out-of-order public disposal supported;
- successful disposal idempotent;
- failed final disposal remains retryable through the same lease;
- session disposal is authoritative cleanup;
- acquisition requires interactive terminal output;
- no support bit, raw mode number, or nesting-depth property.

Workflow #325 was green.

Record: `docs/T93-Public-Synchronized-Output-Lease-and-API.md`.

### T94 — transactional output composition — complete

Accepted composition with:

- `WriteTextAsync`;
- sequential low-level terminfo output;
- OSC 0/1/2 titles;
- OSC 7 locations;
- OSC 8 hyperlinks;
- OSC 52 clipboard operations;
- cursor-style mutation and leases;
- presentation transitions;
- active query requests.

Synchronized output introduces no second serialization domain and no application-side transaction buffer.

The regression suite caught and rejected an attempted recursive gating change to the historical low-level terminfo primitive. The established advanced-output contract was restored before stable closure.

Record: `docs/T94-Synchronized-Output-Composition-and-Ordering.md`.

### T95 — lifecycle, cancellation, failure recovery, and disposal — complete

Proven:

- explicit first-owner begin commit boundary;
- cancellation while waiting for the output gate emits no begin/end bytes;
- begin-write failure triggers immediate best-effort end + flush cleanup;
- begin failure plus cleanup failure retains session-owned cleanup for disposal retry;
- final end-write failure remains retryable through the same lease;
- final flush failure remains retryable through the same lease;
- suspend physically leaves mode 2026 and preserves logical owners;
- resume re-enters only if logical owners remain;
- releasing the final owner while suspended is logical-only and prevents re-entry;
- session disposal performs final best-effort cleanup.

Record: `docs/T95-Synchronized-Output-Lifecycle-Failure-and-Disposal.md`.

### T96 — integration, compatibility, concurrency, and downstream acceptance — complete

Delivered:

- Windows/Linux/macOS regression acceptance;
- 64 simultaneous public owners plus repeated concurrent acquisition/release rounds;
- one physical begin / one final end+flush per logical transaction;
- real downstream acceptance using published `Icod.DCurses 0.1.0` with current Terminal source;
- real `CursesSession.RefreshAsync()` executed between canonical mode-2026 begin/end frames;
- acceptance on net8.0/net9.0/net10.0;
- no fabricated support inference.

Workflow #347 passed normal tests and DCurses acceptance on Windows, Linux, and macOS.

Record: `docs/T96-Synchronized-Output-Integration-Compatibility-and-DCurses-Acceptance.md`.

### T97 — public API, docs, sample, package, and stable closure — complete

Delivered:

- `docs/Public-API-Baseline-0.9.md`;
- root README update;
- focused `Icod.Terminal.SynchronizedOutput.Sample` as a normal `Icod.Terminal.sln` project;
- package XML-documentation assertions;
- fresh package-only consumer smoke on net8.0/net9.0/net10.0;
- stable `0.9.0` metadata;
- PR/distribution/tag gates through the ordinary solution build plus the 0.9 package contract;
- retained 0.8 cursor-style package gate;
- retained real downstream DCurses acceptance in PR/distribution/release validation;
- redirected-output rejection coverage;
- disposal-ordering coverage proving presentation restoration precedes the final synchronized-output end frame;
- `docs/T97-0.9.0-Public-API-Package-and-Stable-Closure.md`.

The merge gate is exact-head validation of the completed release candidate.

---

## 4. Frozen API direction

The public shape is a scoped semantic lease rather than explicit enable/disable methods because ownership is inherently paired and nested:

```csharp
await using TerminalSynchronizedOutputLease lease =
	await session.AcquireSynchronizedOutputAsync();

await session.WriteTextAsync( "..." );
```

A lease represents logical ownership, not proof that the terminal honors DEC private mode 2026.

No public `SetPrivateModeAsync( 2026, true )`, raw `DECSET`, raw `DECRST`, generic CSI API, or inferred support flag is part of 0.9.

---

## 5. Frozen nesting model

Synchronized output uses identity-aware first-owner/last-owner physical transitions:

```text
Acquire A -> emit begin
Acquire B -> no additional begin
Dispose A -> no end
Dispose B -> emit end + flush
```

This differs from cursor-style and hyperlink state replacement because every synchronized-output owner requests the same boolean active state.

Out-of-order disposal is therefore valid.

---

## 6. Frozen flush policy

- first acquisition emits begin without an implicit flush;
- nested acquisition emits nothing and flushes nothing;
- contained operations retain their existing flush semantics;
- non-final release emits nothing and flushes nothing;
- final release emits end and performs one flush;
- lifecycle/disposal cleanup which physically leaves synchronized output also flushes after the leave frame.

The final flush is a library-side transport boundary, not a stronger guarantee about terminal-side rendering behavior.

---

## 7. Frozen capability/query posture

Successful frame emission does not prove synchronized-output support.

0.9 does not infer support from:

- `TERM`;
- operating system;
- terminal-emulator name;
- xterm lineage;
- environment variables.

Ordinary acquisition does not perform DECRQM status observation. Active queries remain legal inside synchronized output and retain their existing caller-visible timeouts and flush semantics.

---

## 8. Frozen lifecycle posture

```text
active logical owners
    |
prepare suspend -> physically leave mode 2026 + flush
    |
logical ownership retained while suspended
    |
resume -> physically re-enter only if owners remain
    |
last owner release -> physically leave + flush
```

Release while suspended updates logical ownership without unnecessary bytes.

---

## 9. Frozen failure-recovery posture

- failure before begin commit produces no terminal mutation;
- begin-write failure after commit may mean mode 2026 became active and therefore triggers best-effort leave;
- acquisition plus cleanup failure reports both failures and retains session cleanup responsibility;
- failure to leave or flush on final release keeps the final lease owned for retry;
- session disposal performs authoritative best-effort cleanup;
- caller cancellation never truncates a committed control frame.

---

## 10. Explicit non-goals

0.9 does not add:

- public generic CSI/DECSET/DECRST construction;
- synchronized output automatically around every write;
- cursor color control;
- modern keyboard protocols;
- shell-integration protocols;
- terminal-emulator configuration-file editing;
- background capability probing;
- generic transaction buffering inside `Icod.Terminal`;
- a replacement for `Icod.DCurses` damage/refresh policy.

Synchronized output controls terminal presentation timing; it does not make `Icod.Terminal` a retained-mode renderer.

---

## 11. Stable release gate

`0.9.0` is ready for stable publication only when:

1. T90–T97 are implemented and documented;
2. mode-2026 framing is structural and byte-exact;
3. nested ownership is deterministic;
4. semantic output composition preserves established ownership contracts;
5. failure paths retain truthful cleanup ownership;
6. suspend/resume/disposal cannot leak library-owned synchronized mode;
7. Windows/Linux/macOS exact stable-head PR validation is green;
8. downstream `Icod.DCurses` acceptance is green;
9. focused synchronized-output sample builds as part of the solution;
10. fresh package-only consumers pass on net8.0/net9.0/net10.0;
11. packaged XML documentation contains the reviewed 0.9 public delta;
12. `main` Release distribution validation is green after merge;
13. only then is tag `v0.9.0` created.

---

## 12. Current stable state

```text
VersionPrefix:   0.9.0
VersionSuffix:   <empty>
Version:         0.9.0
PackageVersion:  0.9.0
AssemblyVersion: 0.9.0.0
```

**T90–T97 are complete. Merge requires exact-head green validation; publication still requires post-merge `main` Release validation.**
