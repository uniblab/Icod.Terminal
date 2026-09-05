# Icod.Terminal 0.9.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.9.0`  
**Development version:** `0.9.0-alpha.1`  
**Predecessor:** `0.8.0` — typed cursor-style control and truthful restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** synchronized output and nested transactional output state  
**Status:** T90 contract freeze in progress

---

## 1. Release objective

`Icod.Terminal 0.9.0` SHALL complete the 0.4–0.9 operational protocol-closure sequence by adding semantic synchronized-output ownership on top of the session output gate, lifecycle machinery, active query substrate, and scoped-state mechanisms established in earlier releases.

The primary protocol target is DEC private mode 2026:

```text
CSI ? 2026 h
CSI ? 2026 l
```

Public callers SHALL work with semantic synchronized-output ownership rather than raw DECSET/DECRST construction.

Synchronized output SHALL remain opt-in. Ordinary terminal writes SHALL NOT silently enable it.

---

## 2. Existing foundations reused by 0.9

0.9 builds on:

- session-owned serialized output;
- complete-frame commit semantics;
- canonical seven-bit CSI output;
- reversible presentation leases;
- strict-LIFO hyperlink and cursor-style ownership patterns;
- lifecycle prepare/suspend/resume participation;
- deterministic session disposal;
- one shared input reader and query transaction manager;
- typed DECRQSS support;
- truthful state restoration policy;
- package-only consumer validation across net8.0/net9.0/net10.0.

No second output transport, reader loop, or raw public CSI surface is introduced.

---

## 3. Tranche sequence

### T90 — synchronized-output contract and reference freeze

Freeze:

- DEC private mode 2026 wire semantics;
- begin/end ownership rules;
- nesting model;
- flush policy;
- cancellation and commit boundary;
- suspend/resume/disposal behavior;
- interaction with unscoped session output;
- interaction with presentation, hyperlink, clipboard, cursor-style, and query output;
- capability/query posture;
- failure-recovery guarantees;
- public API direction.

**Gate T90:** no implementation begins until the ownership and flush contracts are internally consistent.

### T91 — internal synchronized-output CSI primitive

Implement:

- internal canonical seven-bit `CSI ? 2026 h` begin frame;
- internal canonical seven-bit `CSI ? 2026 l` end frame;
- structural private-parameter CSI support as needed without exposing a public generic writer;
- complete-frame construction before commit;
- cancellation-before-commit behavior;
- byte-exact tests.

### T92 — synchronized-output state manager and nesting

Implement:

- session-owned synchronized-output manager;
- deterministic nested ownership;
- first-owner enter / last-owner leave semantics unless T90 freezes a stricter model;
- failure-retained ownership;
- session-disposal cleanup;
- no duplicate physical mode toggles for nested logical owners.

### T93 — public synchronized-output lease/API

Add the reviewed semantic API, expected to resemble:

```csharp
public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
    CancellationToken cancellationToken = default
);
```

Exact naming remains subject to T90.

### T94 — transactional output composition

Prove deterministic composition with:

- `WriteTextAsync`;
- terminfo capability output;
- OSC 0/1/2 titles;
- OSC 7 locations;
- OSC 8 hyperlinks;
- OSC 52 clipboard operations;
- cursor-style mutation and leases;
- presentation transitions;
- active query requests.

The synchronized-output scope must not introduce a second serialization domain.

### T95 — lifecycle, cancellation, failure recovery, and disposal

Prove:

- suspend leaves no library-owned synchronized-output mode active in the shell/parent environment;
- resume re-enters active logical synchronized-output ownership when appropriate;
- release failures remain retryable or cleanup-owned;
- acquisition/release output failures do not silently corrupt ownership state;
- session disposal performs final best-effort leave;
- pre-commit cancellation emits nothing;
- post-commit cancellation cannot truncate a frame.

### T96 — integration, compatibility, and downstream acceptance

Deliver:

- Windows/Linux/macOS regression acceptance;
- nested/ref-counted semantics under concurrent callers;
- output-ordering regression matrix;
- `Icod.DCurses` acceptance showing a full-screen refresh can use synchronized output without private escape sequences;
- compatibility notes for terminals which ignore mode 2026;
- no fabricated support from TERM/emulator identity.

### T97 — public API, docs, sample, package, and stable closure

Deliver:

- `docs/Public-API-Baseline-0.9.md`;
- README update;
- focused synchronized-output sample;
- package XML-documentation assertions;
- fresh package-only consumer smoke on net8.0/net9.0/net10.0;
- stable `0.9.0` metadata;
- final PR/main/tag release gates.

---

## 4. Preliminary API direction

The preferred public shape is a scoped semantic lease rather than explicit enable/disable methods because ownership is inherently paired and nested.

The working direction is:

```csharp
await using TerminalSynchronizedOutputLease lease =
    await session.AcquireSynchronizedOutputAsync();

await session.WriteTextAsync( "..." );
```

A lease SHALL represent logical ownership, not proof that the terminal honors DEC private mode 2026.

No public `SetPrivateModeAsync( 2026, true )`, raw `DECSET`, raw `DECRST`, or generic CSI API is planned.

---

## 5. Preliminary nesting direction

T90 will decide exact mechanics, but the default design target is first-owner/last-owner physical transitions:

```text
Acquire A -> emit begin
Acquire B -> no additional begin
Dispose B -> no end
Dispose A -> emit end
```

This differs from cursor-style and hyperlink strict-LIFO state replacement because synchronized output is one boolean terminal mode with identical desired state for every owner.

Out-of-order disposal MAY therefore be safe if the implementation uses identity-aware reference ownership rather than stack restoration. T90 must freeze this explicitly.

---

## 6. Flush policy to freeze in T90

The release must define whether begin/end perform flushes and at which side of the control frame.

The design goal is that a caller can use a synchronized-output lease to create a terminal-visible transaction without hidden arbitrary flushes between contained writes.

Candidate policy:

- acquisition serializes and emits begin;
- no implicit flush after begin unless protocol evidence requires it;
- contained writes behave according to their existing contracts;
- final release emits end and performs the minimum flush necessary to make the completed transaction visible deterministically;
- nested logical release performs no physical frame or flush.

T90 must freeze this before T91 implementation.

---

## 7. Capability/query posture

Successful frame emission SHALL NOT prove synchronized-output support.

0.9 SHALL NOT infer support from:

- `TERM`;
- operating system;
- terminal-emulator name;
- xterm lineage;
- environment variables.

T90 will determine whether DECRQM status observation for private mode 2026 is required, optional, or deferred. A public lease must remain usable under explicit optimistic-emission semantics if support cannot be queried portably enough.

---

## 8. Lifecycle posture

Library-owned synchronized mode must not leak across managed suspension or session disposal.

T90 is expected to freeze:

```text
active logical owners
    |
prepare suspend -> physically leave mode 2026
    |
resume -> physically re-enter if logical owners remain
    |
last owner release -> physically leave
```

Release while suspended should update logical ownership without emitting unnecessary bytes, following the pattern proven by cursor-style state where applicable.

---

## 9. Failure-recovery posture

The implementation must distinguish logical ownership from physical-write success.

At minimum:

- failure to enter on first acquisition means no lease is returned;
- if entry write may have partially committed before reporting failure, best-effort leave is required;
- failure to leave on final release retains cleanup responsibility;
- disposal retries outstanding cleanup best-effort;
- no caller cancellation may truncate a committed control frame.

The exact retry/aggregate-exception semantics are frozen in T90.

---

## 10. Explicit non-goals

0.9 SHALL NOT add:

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

1. T90 ownership/flush/lifecycle contracts are frozen;
2. mode-2026 framing is structural and byte-exact;
3. nested ownership is deterministic;
4. output composition uses the existing session serialization domain;
5. failure paths retain truthful cleanup ownership;
6. suspend/resume/disposal cannot leak library-owned synchronized mode;
7. Windows/Linux/macOS validation is green;
8. downstream `Icod.DCurses` acceptance is green;
9. package-only consumers pass on net8.0/net9.0/net10.0;
10. packaged XML documentation contains the reviewed 0.9 public delta;
11. `main` Release validation is green after merge;
12. only then is tag `v0.9.0` created.

---

## 12. Current development state

```text
VersionPrefix:   0.9.0
VersionSuffix:   alpha.1
Version:         0.9.0-alpha.1
PackageVersion:  0.9.0-alpha.1
AssemblyVersion: 0.9.0.0
```

**T90 contract freeze is the current tranche.**
