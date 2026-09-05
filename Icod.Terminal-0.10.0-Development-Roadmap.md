# Icod.Terminal 0.10.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.10.0`  
**Current version:** `0.10.0`  
**Predecessor:** `0.9.0` — synchronized output and nested transactional output state  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** safe semantic OSC 9 operations centered on OSC 9;4 terminal progress/activity state  
**Status:** T100–T107 complete; merge requires exact stable-head green validation

---

## 1. Release objective

`Icod.Terminal 0.10.0` adds a reviewed semantic OSC 9;4 progress subsystem without exposing arbitrary OSC or host-control escape construction.

The release provides:

- determinate progress;
- indeterminate progress;
- normal/error/attention semantic states;
- caller-friendly completed/total reporting;
- scoped owned progress state;
- deterministic nested restoration;
- managed suspend/resume participation;
- invalidation and failure recovery;
- cleanup on lease/session disposal;
- composition with synchronized output and existing terminal operations;
- downstream `Icod.DCurses` acceptance.

OSC 9;9 does not create a duplicate public CWD API over the existing OSC 7 operation. Host-executing/blocking OSC 9 commands and OSC 4 palette mutation remain explicit non-goals.

---

## 2. Architectural rules

0.10 preserves the established protocol architecture:

- semantic public APIs rather than arbitrary OSC construction;
- complete-frame construction before commit;
- caller cancellation before commit only;
- non-caller-cancellable committed control frames;
- session-owned output serialization;
- interactive-output requirement for terminal-control operations;
- truthful optimistic support semantics;
- lifecycle participants for scoped terminal state;
- retryable cleanup debt;
- authoritative session-disposal cleanup;
- no second reader or output transport.

No `Icod.TermInfo` change, query parser, response router, or capability-database change is required for OSC 9;4 progress.

---

## 3. Tranche record

| Tranche | Version | Status | Outcome |
| --- | --- | --- | --- |
| T100 | `0.10.0-alpha.1` | Complete | OSC 9 reference/scope and progress ownership contract frozen. |
| T101 | `0.10.0-alpha.2` | Complete | Byte-exact OSC 9;4 writer with canonical BEL termination. |
| T102 | `0.10.0-alpha.3` | Complete | Semantic progress state/value model and overflow-safe stage conversion. |
| T103 | `0.10.0-alpha.4` | Complete | Session-owned ordered progress manager and nested restoration. |
| T104 | `0.10.0-alpha.5` | Complete | Public `TerminalProgressLease` and `AcquireProgressAsync(...)`. |
| T105 | `0.10.0-alpha.6` | Complete | Lifecycle, invalidation, failure, cleanup retry, and disposal hardening. |
| T106 | `0.10.0-alpha.7` | Complete | Composition plus real `Icod.DCurses` progress acceptance. |
| T107 | `0.10.0` | Complete | Public API/package/docs/sample/stable closure. |

---

## 4. T100 — contract/reference freeze

Frozen in `docs/T100-OSC-9-Contract-and-Reference-Freeze.md`:

- canonical wire form `ESC ] 9 ; 4 ; state ; progress BEL`;
- wire states clear/normal/error/indeterminate/attention;
- public semantic `Attention` for vendor wire state 4;
- `long completed, long total` caller model;
- integer nearest-percentage rounding with exact halves upward;
- acquisition emits no frame;
- ordered, identity-aware, out-of-order-safe nesting;
- suspend/session disposal clear progress;
- resume restores current reported owner;
- no support inference/probe;
- OSC 9;9 does not duplicate OSC 7;
- host-control OSC 9 commands excluded;
- OSC 4 palette work orthogonal.

Workflow #376 green.

---

## 5. T101 — byte-exact OSC 9;4 writer

Implemented:

- internal `Osc9ProgressState`;
- specialized encoder/writer;
- canonical BEL termination;
- progress range validation;
- canonical zero for clear/indeterminate;
- one complete non-cancellable committed write;
- no implicit flush;
- byte-exact and cancellation tests.

Record: `docs/T101-OSC-9-4-Byte-Exact-Writer.md`.

---

## 6. T102 — progress value model and stage conversion

Implemented:

- public `TerminalProgressState { Normal, Error, Attention }`;
- internal immutable `TerminalProgressValue`;
- completed/total validation;
- `UInt128` overflow-safe percentage arithmetic;
- deterministic nearest-integer rounding;
- large-count, uneven-stage, boundary, and invalid-input tests.

Corrected cumulative validation green in workflow #386.

Record: `docs/T102-Progress-Value-Model-and-Stage-Conversion.md`.

---

## 7. T103 — session progress manager and nesting

Implemented:

- lazy session-owned manager;
- acquisition order separated from reported-value precedence;
- unreported newer owners do not mask lower progress;
- non-controlling reports are logical-only;
- controlling release restores newest remaining reported owner;
- out-of-order non-controlling release is silent;
- final release clears progress;
- failed transitions retain cleanup debt;
- progress cleanup precedes synchronized-output final leave on session disposal.

Workflow #392 green.

Record: `docs/T103-Session-Progress-Manager-and-Nesting.md`.

---

## 8. T104 — public progress lease/API

Frozen public delta:

```csharp
public enum TerminalProgressState {
	Normal,
	Error,
	Attention
}

public sealed class TerminalProgressLease : IAsyncDisposable {
	public ValueTask ReportAsync(
		long completed,
		long total,
		CancellationToken cancellationToken = default
	);

	public ValueTask ReportAsync(
		TerminalProgressState state,
		long completed,
		long total,
		CancellationToken cancellationToken = default
	);

	public ValueTask SetIndeterminateAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask DisposeAsync();
}

public ValueTask<TerminalProgressLease> AcquireProgressAsync(
	CancellationToken cancellationToken = default
);
```

Workflow #398 green.

Record: `docs/T104-Public-Terminal-Progress-Lease-and-API.md`.

---

## 9. T105 — lifecycle, invalidation, failure, disposal

Implemented and proved:

- suspend clear and resume restoration;
- release-all-while-suspended prevents re-entry;
- `TerminalSession.InvalidateState()` participation;
- invalidated-state recovery before later report/release;
- failed update/clear cleanup debt;
- retryable final lease cleanup;
- retryable manager/session cleanup;
- re-entry plus cleanup double-failure aggregation;
- non-caller-cancellable cleanup.

Workflow #404 green.

Record: `docs/T105-Progress-Lifecycle-Failure-and-Disposal.md`.

---

## 10. T106 — composition and downstream acceptance

Implemented and validated:

- byte-exact composition with text, OSC 0, OSC 7, OSC 8, OSC 52, cursor style, presentation state, synchronized output, and active queries;
- real downstream consumer using published `Icod.DCurses 0.1.0` and current Terminal source;
- stage 1/10 and 2/10 reports around real `CursesSession.RefreshAsync()`;
- indeterminate progress around a real refresh;
- attention-state report and final clear;
- net8.0/net9.0/net10.0 acceptance;
- PR/distribution/tagged-release integration.

Record: `docs/T106-Progress-Composition-and-DCurses-Acceptance.md`.

---

## 11. T107 — public API, docs, sample, package, stable closure

Completed:

- `docs/Public-API-Baseline-0.10.md`;
- root README stable 0.10 documentation;
- solution-owned `Icod.Terminal.Progress.Sample`;
- `samples/README.md` progress documentation;
- package release notes/tags;
- fresh package-only progress consumer on net8.0/net9.0/net10.0;
- XML-documentation assertions for the complete 0.10 public delta including enum members;
- retained 0.8 and 0.9 package-contract gates;
- 0.10 progress package gate in PR/distribution/release validation;
- explicit redirected-output rejection coverage;
- explicit session-disposal ordering coverage;
- stable `0.10.0` metadata.

Remaining release sequence:

1. exact stable PR head green;
2. merge to `main`;
3. Release distribution validation green on exact `main`;
4. only then tag `v0.10.0`.

---

## 12. Current stable state

```text
VersionPrefix:   0.10.0
VersionSuffix:   <empty>
Version:         0.10.0
PackageVersion:  0.10.0
AssemblyVersion: 0.10.0.0
```

**T100–T107 are complete. Merge requires exact stable-head green validation; publication still requires post-merge `main` Release validation before `v0.10.0`.**
