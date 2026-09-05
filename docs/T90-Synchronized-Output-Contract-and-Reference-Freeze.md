# T90 — Synchronized Output Contract and Reference Freeze

**Release:** `0.9.0`  
**Tranche:** `T90`  
**Development version:** `0.9.0-alpha.1`  
**Status:** Contract freeze in progress  
**Theme:** DEC private mode 2026, nested ownership, output transaction boundaries, lifecycle, and failure recovery

## 1. Protocol target

The 0.9 synchronized-output feature targets DEC private mode 2026 using canonical seven-bit CSI framing:

```text
CSI ? 2026 h
CSI ? 2026 l
```

Canonical bytes are therefore:

```text
ESC [ ? 2 0 2 6 h
ESC [ ? 2 0 2 6 l
```

The library does not expose raw private-mode numbers or a generic DECSET/DECRST API.

## 2. Semantic meaning

Synchronized output is modeled as one terminal boolean presentation-timing mode:

- **inactive** — terminal may present incremental output normally;
- **active** — terminal is requested to defer presentation until synchronized output is released according to its implementation of private mode 2026.

Successful emission proves only that the complete frame was written. It does not prove that the terminal implements or honors synchronized output.

## 3. Public ownership direction

The preferred public API is a scoped lease:

```csharp
public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
    CancellationToken cancellationToken = default
);
```

The lease owns one logical request for synchronized output.

A public `EnableSynchronizedOutputAsync`, `DisableSynchronizedOutputAsync`, raw mode integer, or generic private-mode API is rejected for 0.9 because they make paired ownership and failure cleanup easier to misuse.

Exact public names remain provisional until T93.

## 4. Nesting model

Unlike cursor-style or hyperlink leases, every synchronized-output owner requests the same boolean active state. The natural ownership model is therefore identity-aware first-owner/last-owner semantics rather than strict state-replacement LIFO.

Target behavior:

```text
Acquire A -> emit CSI ? 2026 h
Acquire B -> no physical frame
Acquire C -> no physical frame
Dispose B -> no physical frame
Dispose C -> no physical frame
Dispose A -> emit CSI ? 2026 l
```

Out-of-order disposal is therefore acceptable provided ownership identity is tracked correctly and the final physical leave occurs only when the last active logical owner is released.

This is intentionally different from cursor-style restoration, where nested owners request different states and strict LIFO is necessary.

## 5. Output serialization

All physical begin/end frames use the existing `TerminalSession` output serialization domain.

There is no second output gate.

While synchronized-output ownership is active, ordinary session writes continue to acquire and release the existing output gate normally. The lease does not monopolize that gate for its entire lifetime; doing so would deadlock unrelated semantic operations and query requests.

The synchronized-output manager owns only the logical mode state and serializes mode transitions relative to other session-owned output.

## 6. Flush policy

The working contract is:

- first acquisition emits `CSI ? 2026 h` without an implicit flush after the frame;
- nested acquisitions emit nothing and flush nothing;
- contained ordinary writes preserve their pre-existing flush semantics;
- non-final releases emit nothing and flush nothing;
- final release emits `CSI ? 2026 l` and then performs one flush so the completed synchronized update has a deterministic visibility boundary;
- session cleanup which physically leaves synchronized mode also flushes after the leave frame.

The begin frame is not followed by a flush because flushing after begin does not improve transaction completeness and would add an unnecessary boundary before the synchronized content.

T91/T92 tests must prove the exact flush counts.

## 7. Cancellation and commit boundary

Acquisition and ordinary release follow the established complete-frame policy:

- argument/state validation precedes output;
- caller cancellation before first-owner begin commit prevents any frame;
- the complete begin/end frame is constructed before output commit;
- once a physical begin/end write is committed, caller cancellation cannot truncate that frame;
- cleanup/release uses non-caller-cancellable output after cleanup ownership has been established.

Nested acquisitions which require no physical transition may still observe caller cancellation before logical ownership is committed.

## 8. Acquisition failure

First-owner acquisition is transactional.

If the begin write reports failure after emission was attempted:

1. no public lease is returned;
2. the manager treats the physical mode as potentially active;
3. it performs a best-effort `CSI ? 2026 l` cleanup using non-caller-cancellable output;
4. if cleanup succeeds, the original acquisition failure is rethrown;
5. if cleanup also fails, both failures are reported and cleanup ownership remains with the session for disposal retry.

Nested acquisition does not emit a begin frame and therefore cannot create a new physical cleanup obligation.

## 9. Release failure

When the last logical owner releases:

1. the manager attempts `CSI ? 2026 l`;
2. ownership is not considered physically cleared until the leave frame and required flush succeed;
3. if leave or flush fails, cleanup responsibility remains retained;
4. repeated lease disposal or session disposal may retry the cleanup;
5. logical ownership bookkeeping must not produce duplicate live owners after a failed cleanup attempt.

Exact retry object semantics are implemented in T92/T93 and tested in T95.

## 10. Lifecycle contract

Library-owned synchronized output must not leak into the shell, parent terminal context, or suspended process environment.

With active logical owners:

```text
active owners
    |
prepare suspend
    v
emit leave + flush
    |
process suspended
    |
resume/session re-entry
    v
emit begin if logical owners remain
```

The logical owner set survives managed suspension.

If all owners are released while suspended, release is logical-only and resume must not re-enter synchronized mode.

If leave-on-suspend fails, session state cannot be considered safely quiesced. If re-entry fails, the session must not claim valid restored state.

## 11. Session disposal contract

Session disposal is authoritative cleanup.

If synchronized output may still be physically active, disposal performs best-effort leave and flush before completing terminal restoration.

Outstanding lease objects are invalidated by owner cleanup so later disposal of those lease objects performs no additional output.

Disposal must not introduce a query or capability probe.

## 12. Capability and support posture

The 0.9 synchronized-output lease is optimistic semantic emission, not a support guarantee.

The library SHALL NOT infer synchronized-output support from:

- `TERM`;
- operating system;
- terminal-emulator identity;
- xterm lineage;
- environment variables.

No `SupportsSynchronizedOutput` boolean is fabricated.

DECRQM/private-mode status querying is not required for ordinary acquisition in the initial 0.9 contract. T90 leaves room for an explicit typed observation API later in 0.9 only if it can reuse the existing single-reader query architecture cleanly and if reference behavior is sufficiently portable.

Ordinary lease acquisition does not query before emission.

## 13. Interaction with existing semantic output

While synchronized output is active, these operations remain legal and remain ordered through the shared session output gate:

- application text;
- terminfo capability output;
- presentation transitions;
- OSC 0/1/2 title operations;
- OSC 7 current-location publication;
- OSC 8 hyperlink begin/end and scoped restoration;
- OSC 52 clipboard writes and explicit queries;
- cursor-style setters and leases;
- active query request frames.

No existing semantic operation is silently buffered by `Icod.Terminal`; synchronized output only requests terminal-side presentation deferral.

## 14. Queries inside synchronized output

Active terminal queries remain permitted while synchronized-output ownership is active.

The query request frame is ordinary session-owned output and must serialize through the shared output gate.

The library must not assume that synchronized output blocks terminal responses. If empirical compatibility evidence later shows a query family cannot be used safely while mode 2026 is active, that restriction must be explicit and protocol-specific rather than globally inferred.

## 15. Presentation and Icod.DCurses interaction

`TerminalPresentationLease` and synchronized-output ownership are orthogonal.

A full-screen presentation lease may be active before, during, or after synchronized-output ownership.

`Icod.DCurses` should eventually be able to wrap one refresh transaction with synchronized output without owning private CSI sequences itself:

```text
Acquire synchronized output
write refresh diff
release synchronized output
```

0.9 does not move cell buffering, damage tracking, or rendering policy into `Icod.Terminal`.

## 16. Concurrency posture

Multiple callers may acquire synchronized-output leases concurrently.

The manager must serialize ownership mutation independently from the byte-level output gate while avoiding lock-order inversion.

A manager lock may be held while a physical transition is emitted, but no caller-visible lease may require holding the byte-output gate for its full lifetime.

T92 must document and test the lock ordering explicitly.

## 17. Expected public delta

The minimum expected public delta is:

```csharp
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable {
    public ValueTask DisposeAsync();
}

public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
    CancellationToken cancellationToken = default
);
```

No public property is required merely to expose an implementation detail such as nesting depth.

A future explicit observation type is not frozen by T90.

## 18. Deliberate omissions

T90 excludes:

- public raw private-mode numbers;
- public generic CSI/DECSET/DECRST writers;
- automatic synchronized output around every session write;
- automatic synchronized output around every presentation lease;
- terminal-side support inference;
- background support probing;
- caller-provided mode numbers;
- generic in-memory output transaction buffering;
- nested transaction payload objects;
- changes to cursor visibility/style ownership;
- changes to hyperlink ownership semantics.

## 19. T90 acceptance criteria

T90 is frozen when the implementation tranches can proceed without unresolved ambiguity in:

1. exact begin/end bytes;
2. logical nesting semantics;
3. out-of-order disposal behavior;
4. output serialization domain;
5. begin/end flush policy;
6. cancellation-before-commit behavior;
7. acquisition-write failure recovery;
8. final-release failure ownership;
9. suspend/resume behavior;
10. release-while-suspended behavior;
11. session-disposal cleanup;
12. support/capability uncertainty;
13. composition with existing semantic output and active queries;
14. downstream `Icod.DCurses` boundary.

Implementation work begins with T91 only after this contract is reviewed and accepted.
