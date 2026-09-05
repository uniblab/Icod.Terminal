# T100 — OSC 9 Contract and Reference Freeze

**Release:** `0.10.0`  
**Tranche:** `T100`  
**Development version:** `0.10.0-alpha.1`  
**Status:** Contract frozen

---

## 1. Purpose

T100 freezes the safe semantic OSC 9 subset for `Icod.Terminal 0.10.0` before implementation begins.

The mandatory protocol is OSC 9;4 progress/activity state. The release treats OSC 9 as a vendor-extension namespace whose subcommands must be classified individually rather than as one uniformly safe API family.

Primary references:

- Microsoft Learn, **Tutorial: Set the progress bar in the Windows Terminal** — `https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences`
- ConEmu, **ANSI X3.64 and Xterm-256 Support / ConEmu specific OSC** — `https://conemu.github.io/en/AnsiEscapeCodes.html`

---

## 2. OSC 9;4 wire grammar

The canonical outbound progress frame is:

```text
ESC ] 9 ; 4 ; <state> ; <progress> BEL
```

where:

- `ESC` is byte `0x1b`;
- `]` is byte `0x5d`;
- separators are ASCII semicolons;
- `<state>` is one decimal digit from `0` through `4`;
- `<progress>` is an ASCII decimal integer from `0` through `100`;
- `BEL` is byte `0x07` and is the canonical 0.10 terminator.

ConEmu accepts BEL or ST termination. Windows Terminal's documented OSC 9;4 format uses BEL. `Icod.Terminal 0.10` therefore emits BEL canonically for the documented common subset.

No caller may supply a raw OSC 9 payload or terminator.

---

## 3. Frozen semantic state mapping

| Wire state | `Icod.Terminal` semantic meaning | Notes |
| ---: | --- | --- |
| `0` | clear | Removes library-owned progress/activity indication. |
| `1` | normal | Determinate/default progress. |
| `2` | error | Determinate error progress. |
| `3` | indeterminate | Progress value is semantically ignored; 0 is emitted canonically. |
| `4` | attention | Neutral library term for the vendor-dependent warning/paused rendering. |

Microsoft describes wire state `4` as **Warning**. ConEmu describes it as **paused**. The public semantic term SHALL therefore be `Attention` rather than either vendor-specific label.

The clear state is an operation/physical state, not a public active-progress state value.

---

## 4. Determinate progress model

Public callers SHALL be able to report completed work as an integral fraction:

```text
completed / total
```

rather than being forced to compute wire percentages.

Frozen validation:

```text
total > 0
0 <= completed <= total
```

The public completed/total representation SHALL use signed 64-bit integral values so the same API can represent stages, item counts, byte counts, or other large finite workloads.

Examples:

```text
1 / 10  -> 10%
2 / 10  -> 20%
1 / 3   -> 33%
2 / 3   -> 67%
3 / 3   -> 100%
```

Percentage conversion SHALL:

- use integer arithmetic;
- round to nearest integer percentage;
- map exact half cases upward;
- avoid arithmetic overflow for all valid `long` inputs;
- clamp only by validation semantics, never silently repair invalid completed/total values.

No floating-point arithmetic is required by the contract.

---

## 5. Progress value semantics

An active progress owner has one current logical value in one of these forms:

```text
Determinate(state, completed, total)
Indeterminate
```

Determinate semantic state SHALL be one of:

```text
Normal
Error
Attention
```

`Indeterminate` is its own semantic mode rather than a determinate state with a fabricated percentage.

A progress lease MAY be acquired before its first report. Acquisition by itself SHALL NOT emit a progress frame. This permits a caller to establish ownership first and then choose determinate or indeterminate state explicitly.

---

## 6. Public API direction

The reviewed public direction is a scoped semantic owner rather than raw setters.

Expected shape:

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

Exact public names remain reviewable through T104, but the semantic responsibilities above are frozen.

A raw public `SendOsc9(...)`, `SetOsc9Subcommand(...)`, or generic OSC writer SHALL NOT be introduced.

---

## 7. Ownership and nesting

Progress is persistent terminal chrome state and therefore SHALL be session-owned.

`Icod.Terminal` cannot truthfully query an arbitrary pre-existing OSC 9;4 progress state. The outermost library-owned progress scope therefore owns from an implicit baseline of **no Icod-owned progress state**.

Nested ownership SHALL be identity-aware and ordered by acquisition:

```text
Acquire A
A reports 30%          -> emit A 30%

Acquire B
B indeterminate        -> emit indeterminate

Dispose B              -> restore A 30%
A reports 40%          -> emit A 40%

Dispose A              -> clear progress
```

Out-of-order disposal SHALL be safe:

- disposing a non-top owner removes that logical owner without changing physical state;
- disposing the physically controlling/top owner restores the most recently acquired remaining owner which has a current logical value;
- if no remaining owner has a reported value, progress is cleared;
- final owner disposal clears progress.

An owner which has never reported a value does not mask a lower owner's reported value.

This provides stack-like state restoration without requiring strict-LIFO disposal.

---

## 8. Update semantics

Each progress update SHALL:

1. validate all public arguments before protocol emission;
2. compute the complete logical/wire value;
3. construct the entire OSC frame before commit;
4. honor caller cancellation before commit;
5. acquire the existing session output serialization domain;
6. once transmission begins, write the complete frame without caller-driven cancellation;
7. update manager physical-state knowledge only after successful write completion.

Progress updates SHALL NOT implicitly flush unless an existing containing operation does so for its own contract.

A successful write proves only that the complete OSC 9;4 frame was emitted. It does not prove that the terminal recognized or rendered the progress state.

---

## 9. Failure semantics

A failed update SHALL NOT silently advance logical physical-state knowledge.

If a write reports failure after transmission was attempted, physical terminal state may be uncertain. The manager SHALL retain cleanup responsibility where necessary rather than assuming the terminal remained unchanged.

Cleanup operations use non-caller-cancellable complete-frame writes.

Failure to clear on final release or lifecycle cleanup SHALL retain cleanup responsibility for retry/session disposal according to the same truthful-cleanup principles established by 0.9 synchronized output.

---

## 10. Managed lifecycle

With active reported progress:

```text
prepare suspend -> emit clear
suspended       -> retain logical owners/values only
resume          -> restore current controlling logical value if one remains
```

If all progress owners are released while suspended, resume SHALL emit no progress frame.

Progress acquisition while session state is suspended SHALL be rejected rather than silently creating a state that cannot be physically established.

Session disposal SHALL perform authoritative best-effort clear and invalidate outstanding progress leases so late disposal emits nothing.

---

## 11. Composition with synchronized output

OSC 9;4 progress uses the existing session output serialization domain and SHALL NOT create another transport or lock domain.

Progress updates are legal while an OSC 9 progress lease and a `TerminalSynchronizedOutputLease` are both active.

No special terminal-side atomicity guarantee is made for tab/taskbar progress rendering while DEC private mode 2026 is active; that behavior belongs to the terminal emulator.

`Icod.Terminal` guarantees only deterministic byte ordering among its own operations.

---

## 12. Support posture

0.10 uses optimistic emission.

The library SHALL NOT infer OSC 9;4 support from:

- operating system;
- `TERM`;
- Windows Terminal environment variables;
- ConEmu environment variables;
- emulator process names;
- xterm ancestry.

No automatic support probe is introduced.

Unsupported terminals are expected to ignore the OSC extension according to their own behavior. Successful API completion means emission, not observed support.

---

## 13. OSC 9;9 current-working-directory disposition

ConEmu documents OSC 9;9 as current-working-directory publication.

`Icod.Terminal` already exposes semantic current-location publication through OSC 7. Therefore 0.10 SHALL NOT add a second public CWD API merely to emit OSC 9;9.

T106 MAY add an internal or compatibility emission strategy only if downstream evidence demonstrates a meaningful terminal-compatibility benefit which cannot be achieved through the existing OSC 7 contract.

Default public location publication remains the established OSC 7 API.

---

## 14. Explicit OSC 9 non-goals

The following ConEmu-specific OSC 9 commands are excluded from the 0.10 public contract:

- 9;1 sleep/blocking;
- 9;2 native GUI message box;
- 9;3 ConEmu-specific tab text mutation;
- 9;5 host wait-key;
- 9;6 GUI macro execution;
- 9;7 host process launch;
- 9;8 host environment-variable access;
- 9;10 emulator-global xterm-mode changes;
- 9;11 comments as a public operation;
- 9;12 prompt marking as part of the progress tranche.

These operations either execute/control host behavior, duplicate safer existing semantics, or belong to a future shell-integration milestone.

Their exclusion is deliberate and does not prevent later separately reviewed features.

---

## 15. OSC 4 relationship

OSC 9;4 is not OSC 4.

OSC 4 is the xterm-family palette-color protocol. `Icod.TermInfo` already describes standard color capabilities including `colors`, `pairs`, `initc`, `orig_colors`, and related terminfo operations. Typed OSC 4 palette mutation in `Icod.Terminal` is orthogonal and is not required for OSC 9 progress.

No OSC 4 work is part of T100–T107 unless separately approved.

---

## 16. T100 freeze decision

T100 is frozen with these primary decisions:

1. OSC 9;4 is the mandatory 0.10 protocol.
2. Canonical wire termination is BEL.
3. Wire state 4 maps to neutral semantic `Attention`.
4. Public determinate progress accepts `long completed, long total` and computes a rounded 0–100 percentage internally.
5. Indeterminate progress is explicit and emits state 3 with canonical progress 0.
6. Lease acquisition alone emits nothing.
7. Progress is scoped session-owned state with ordered, identity-aware, out-of-order-safe nesting.
8. Final release, suspend, and session disposal clear library-owned progress.
9. Resume restores current logical progress only when an active reported owner remains.
10. No automatic support inference or probing is added.
11. OSC 9;9 does not create a duplicate public CWD API.
12. Host-executing/blocking OSC 9 commands are explicit non-goals.
13. OSC 4 palette mutation is orthogonal and deferred.

T101 may begin from this contract.
