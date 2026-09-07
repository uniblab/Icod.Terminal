# T191 — Permanent Architecture and Ownership Documentation

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor tranche:** T190 — contract/regret audit, workflow #911  
**Status:** Implementation complete; exact-head validation pending

## 1. Purpose

T191 replaces historical development records as the primary authority for the 1.x architecture, session ownership, lifecycle, and restoration model.

Historical roadmaps and `Txxx` records remain useful design evidence. They are not required reading for consumers and do not override the permanent documents when historical wording reflects an earlier development stage.

## 2. Permanent authorities added

T191 adds:

- `docs/Architecture.md`;
- `docs/Terminal-Session-and-Ownership.md`;
- `docs/Lifecycle-and-Restoration.md`.

Together they define the architecture/ownership portion of the 1.x contract.

## 3. Normative vs implementation detail

The permanent documents deliberately separate behavioral guarantees from current implementation mechanisms.

Examples of **normative** 1.x guarantees include:

- `Icod.TermInfo` remains the immutable capability authority;
- `Icod.Terminal` owns the live terminal conversation and reversible terminal state;
- `Icod.DCurses` owns virtual-screen/window/refresh policy;
- PTY/ConPTY process ownership remains outside `Icod.Terminal`;
- a live session has one authoritative input-reader/query-correlation path;
- caller-supplied transports/providers remain borrowed rather than session-owned object lifetime;
- restoration uses captured/observed state where the public contract promises restoration;
- state uncertainty is surfaced rather than silently advanced;
- public state acquisition cannot enter after lifecycle/teardown has released terminal state;
- lifecycle participant ordering and query restrictions are part of the supported lifecycle contract;
- final asynchronous session disposal remains restoration authority.

Examples of **implementation details** that may evolve compatibly include:

- the current manager class names;
- use of `SemaphoreSlim` or `ConditionalWeakTable` to implement composition;
- internal lifecycle source/signal types;
- private parser/query-transaction classes;
- the exact internal function decomposition of suspend/resume/rollback.

## 4. T190 correction integrated into permanent docs

The permanent architecture now incorporates the sole T190 D-class correction:

- `ITerminalInput` remains public for custom transport injection;
- `TerminalSession.Input` is not public in the 1.0 surface;
- `ReadEventAsync(...)` and typed query APIs form the session-owned input path.

The output side is intentionally different:

- `TerminalSession.Output` remains an advanced borrowed transport;
- direct use is outside session serialization and is caller responsibility;
- ordinary consumers should prefer session-managed output APIs.

This distinction is no longer discoverable only from release-specific audit notes; it is part of the permanent ownership documentation.

## 5. Lifecycle contract consolidated

`Lifecycle-and-Restoration.md` consolidates behavior previously spread across early lifecycle work, color/keyboard lifecycle extensions, 0.17 composition work, and 0.18 hardening.

It now provides one durable explanation of:

- baseline capture;
- POSIX/Windows restoration timing;
- lifecycle support detection;
- lifecycle event meanings;
- termination token semantics;
- suspend release ordering;
- participant prepare/resume ordering;
- query restrictions inside participant resume;
- invalidation and `IsStateValid`;
- lifecycle query generations;
- re-entry rollback/failure semantics;
- disposal and restoration authority.

## 6. Architecture contract consolidated

`Architecture.md` defines three public abstraction levels:

1. ordinary semantic session APIs;
2. advanced transport/provider/native contracts;
3. internal wire/platform machinery.

This prevents the presence of public low-level provider interfaces from being mistaken for a recommendation that normal applications bypass session semantics.

The document also freezes:

- capability-driven rather than brand-driven behavior;
- managed-first/narrow-native interop;
- no process-global current terminal requirement;
- same-physical-terminal overlapping sessions as a caller coordination concern;
- no PTY/process/application-policy creep into the 1.0 layer.

## 7. Session ownership contract consolidated

`Terminal-Session-and-Ownership.md` makes the borrowed-object/state-owner distinction explicit and documents:

- what the session owns;
- what the caller continues to own;
- endpoint/observation roles;
- open-time options policy;
- raw input exclusion and migration;
- advanced raw output boundary;
- application text vs terminfo string output;
- lease ownership and invalidation;
- lifecycle/teardown acquisition barriers;
- disposal semantics;
- exact restoration vs reset policy;
- multiple-session limitations.

## 8. Remaining documentation tranches

T191 intentionally does not duplicate later permanent authorities.

T192 owns:

- `Input-and-Events.md`;
- `Queries-and-Responses.md`;
- traditional/Kitty/xterm input semantics and parser/query bounds.

T193 owns:

- `Presentation-and-Reversible-State.md`;
- `Semantic-Output-Protocols.md`;
- `Security-and-Privacy.md`.

T195 owns:

- `Compatibility-and-Versioning.md`;
- `Migration-to-1.0.md`.

T194/T196 reconcile XML, samples, README/package metadata, and exact public/package baselines against these permanent authorities.

## 9. Exit criteria

T191 is complete when:

1. the three permanent documents exist and agree with current source behavior;
2. the T190 D-class input-ownership correction is represented permanently;
3. architecture boundaries are understandable without historical tranche documents;
4. lifecycle participant ordering and restoration semantics are consolidated;
5. normative behavior is distinguished from implementation detail;
6. Windows/Linux/macOS PR validation and retained downstream/package gates remain green at the exact T191 head.

No new runtime feature or protocol is part of T191.
