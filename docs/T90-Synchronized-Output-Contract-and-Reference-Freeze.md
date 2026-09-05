# T90 — Synchronized Output Contract and Reference Freeze

**Release:** `0.9.0`  
**Tranche:** `T90`  
**Development version:** `0.9.0-alpha.1`  
**Status:** Contract frozen  
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

## 2. Reference semantics

The de-facto synchronized-output specification defines mode 2026 as a presentation-timing hint. While the mode is active, the terminal continues processing incoming text and control sequences while rendering continues to show the last rendered state. Leaving the mode permits the renderer to present the latest processed screen state.

Therefore:

- synchronized output does not pause terminal parsing;
- synchronized output does not create an application-side byte buffer in `Icod.Terminal`;
- active queries remain protocol-legal while mode 2026 is active;
- successful begin/end emission proves only protocol emission, not terminal-side atomic rendering;
- terminal implementations may independently terminate synchronized presentation deferral because of timeout, buffer limits, resize, or other implementation policy.

No `Icod.Terminal` lease lifetime can guarantee that a terminal remains physically synchronized for the entire logical lease lifetime.

## 3. Reference set

T90 is frozen against:

- the Contour synchronized-output extension documentation for mode 2026 semantics, DECRQM detection, begin/end sequences, and implementation-defined timeout posture;
- the original iTerm2 synchronized-updates design rationale and timeout/fail-open discussion;
- existing 0.8 `Icod.Terminal` complete-frame output, lifecycle, query, and truthful-cleanup contracts.

The relevant feature-detection request is:

```text
CSI ? 2026 $ p
```

DECRPM state values are interpreted by the reference as:

```text
0 -> mode not recognized / unsupported
1 -> set / supported and active
2 -> reset / supported and inactive
3 -> permanently set / undefined for synchronized-output use
4 -> permanently reset / unsupported
```

T90 does not expose this as a public API and does not make it an acquisition prerequisite.

## 4. Semantic meaning

Synchronized output is modeled as one terminal boolean presentation-timing mode:

- **inactive** — terminal may present incremental output normally;
- **active** — terminal is requested to defer presentation until synchronized output is released according to its implementation of private mode 2026.

Successful emission proves only that the complete frame was written. It does not prove that the terminal implements or honors synchronized output.

## 5. Public ownership direction

The preferred public API is a scoped lease:

```csharp
public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
    CancellationToken cancellationToken = default
);
```

The lease owns one logical request for synchronized output.

A public `EnableSynchronizedOutputAsync`, `DisableSynchronizedOutputAsync`, raw mode integer, or generic private-mode API is rejected for 0.9 because they make paired ownership and failure cleanup easier to misuse.

Exact public names remain provisional until T93.

## 6. Nesting model

Unlike cursor-style or hyperlink leases, every synchronized-output owner requests the same boolean active state. The ownership model is identity-aware first-owner/last-owner semantics rather than strict state-replacement LIFO.

Frozen behavior:

```text
Acquire A -> emit CSI ? 2026 h
Acquire B -> no physical frame
Acquire C -> no physical frame
Dispose B -> no physical frame
Dispose C -> no physical frame
Dispose A -> emit CSI ? 2026 l
```

Out-of-order disposal is acceptable provided ownership identity is tracked correctly and the final physical leave occurs only when the last active logical owner is released.

Nested acquisitions deliberately do not emit repeated begin frames. This avoids relying on terminal-specific behavior for a second `CSI ? 2026 h` received while synchronized output is already active.

This is intentionally different from cursor-style restoration, where nested owners request different states and strict LIFO is necessary.

## 7. Output serialization

All physical begin/end frames use the existing `TerminalSession` output serialization domain.

There is no second output gate.

While synchronized-output ownership is active, ordinary session writes continue to acquire and release the existing output gate normally. The lease does not monopolize that gate for its entire lifetime; doing so would deadlock unrelated semantic operations and query requests.

The synchronized-output manager owns only the logical mode state and serializes mode transitions relative to other session-owned output.

## 8. Flush policy

The frozen contract is:

- first acquisition emits `CSI ? 2026 h` without an implicit flush after the frame;
- nested acquisitions emit nothing and flush nothing;
- contained ordinary writes preserve their pre-existing flush semantics;
- non-final releases emit nothing and flush nothing;
- final release emits `CSI ? 2026 l` and then performs one flush so the completed synchronized update has a deterministic library-side transport boundary;
- session cleanup which physically leaves synchronized mode also flushes after the leave frame.

The final flush guarantees only that `Icod.Terminal` has asked its output service to flush after emitting ESU. It does not strengthen the terminal-side rendering guarantee beyond mode 2026 itself.

T91/T92 tests must prove the exact flush counts.

## 9. Cancellation and commit boundary

Acquisition and ordinary release follow the established complete-frame policy:

- argument/state validation precedes output;
- caller cancellation before first-owner begin commit prevents any frame;
- the complete begin/end frame is constructed before output commit;
- once a physical begin/end write is committed, caller cancellation cannot truncate that frame;
- cleanup/release uses non-caller-cancellable output after cleanup ownership has been established.

Nested acquisitions which require no physical transition still observe caller cancellation before logical ownership is committed.

## 10. Acquisition failure

First-owner acquisition is transactional.

If the begin write reports failure after emission was attempted:

1. no public lease is returned;
2. the manager treats the physical mode as potentially active;
3. it performs a best-effort `CSI ? 2026 l` cleanup using non-caller-cancellable output;
4. cleanup includes the required final flush;
5. if cleanup succeeds, the original acquisition failure is rethrown;
6. if cleanup also fails, both failures are reported and cleanup ownership remains with the session for disposal retry.

Nested acquisition does not emit a begin frame and therefore cannot create a new physical cleanup obligation.

## 11. Release failure

When the last logical owner releases:

1. the manager attempts `CSI ? 2026 l`;
2. it then performs the required flush;
3. ownership is not considered physically cleared until both steps succeed;
4. if leave or flush fails, cleanup responsibility remains retained;
5. repeated lease disposal or session disposal may retry the cleanup;
6. logical ownership bookkeeping must not produce duplicate live owners after a failed cleanup attempt.

Exact retry object semantics are implemented in T92/T93 and tested in T95.

## 12. Lifecycle contract

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

No hidden synchronized-output timeout is implemented by `Icod.Terminal`. Terminal-side implementations may independently end synchronized presentation deferral according to their own safety policy; the library still emits the matching ESU when logical ownership ends.

## 13. Session disposal contract

Session disposal is authoritative cleanup.

If synchronized output may still be physically active, disposal performs best-effort leave and flush before completing terminal restoration.

Outstanding lease objects are invalidated by owner cleanup so later disposal of those lease objects performs no additional output.

Disposal must not introduce a query or capability probe.

## 14. Capability and support posture

The 0.9 synchronized-output lease is optimistic semantic emission, not a support guarantee.

The library SHALL NOT infer synchronized-output support from:

- `TERM`;
- operating system;
- terminal-emulator identity;
- xterm lineage;
- environment variables.

No `SupportsSynchronizedOutput` boolean is fabricated.

DECRQM/private-mode status querying is not required for ordinary acquisition. T90 does not freeze a public synchronized-output observation API. Such an API may be considered later only if it cleanly reuses the existing single-reader query architecture and provides material value beyond optimistic emission.

Ordinary lease acquisition does not query before emission.

## 15. Interaction with existing semantic output

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

## 16. Queries inside synchronized output

Active terminal queries remain permitted while synchronized-output ownership is active because the reference semantics continue terminal parsing while rendering is deferred.

The query request frame is ordinary session-owned output and must serialize through the shared output gate.

A terminal implementation which delays or suppresses query responses while mode 2026 is active may cause the existing caller-visible query timeout to expire. `Icod.Terminal` does not globally prohibit queries merely to accommodate such terminal-specific behavior.

## 17. Presentation and Icod.DCurses interaction

`TerminalPresentationLease` and synchronized-output ownership are orthogonal.

A full-screen presentation lease may be active before, during, or after synchronized-output ownership.

`Icod.DCurses` should eventually be able to wrap one refresh transaction with synchronized output without owning private CSI sequences itself:

```text
Acquire synchronized output
write refresh diff
release synchronized output
```

0.9 does not move cell buffering, damage tracking, or rendering policy into `Icod.Terminal`.

## 18. Concurrency posture

Multiple callers may acquire synchronized-output leases concurrently.

The manager must serialize ownership mutation independently from the byte-level output gate while avoiding lock-order inversion.

A manager lock may be held while a physical transition is emitted, but no caller-visible lease may require holding the byte-output gate for its full lifetime.

T92 must document and test the lock ordering explicitly.

## 19. Expected public delta

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

No public observation type is frozen by T90.

## 20. Deliberate omissions

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
- hidden library-side synchronized-output timers;
- changes to cursor visibility/style ownership;
- changes to hyperlink ownership semantics.

## 21. T90 acceptance criteria

T90 is frozen because the implementation tranches can proceed without unresolved ambiguity in:

1. exact begin/end bytes;
2. reference semantics and implementation-defined terminal timeout behavior;
3. logical nesting semantics;
4. out-of-order disposal behavior;
5. output serialization domain;
6. begin/end flush policy;
7. cancellation-before-commit behavior;
8. acquisition-write failure recovery;
9. final-release failure ownership;
10. suspend/resume behavior;
11. release-while-suspended behavior;
12. session-disposal cleanup;
13. support/capability uncertainty;
14. composition with existing semantic output and active queries;
15. downstream `Icod.DCurses` boundary.

Implementation work proceeds with T91.
