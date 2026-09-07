# Presentation and Reversible State

This document is the permanent 1.x contract for terminal presentation ownership and reversible terminal-side state managed by `Icod.Terminal`.

The key rule is that **not all terminal state has the same restoration semantics**. The library distinguishes exact observed restoration, terminal-policy reset, library-owned nested state, and ephemeral metadata. Consumers must not treat those categories as interchangeable.

## 1. State categories

### 1.1 Exact-restoration state

An exact-restoration operation first establishes a truthful baseline, then owns a mutation, and later restores the observed baseline rather than guessing a default.

Examples include:

- native terminal input mode captured at `TerminalSession` open;
- scoped cursor style;
- scoped palette color;
- scoped dynamic color.

If the required baseline cannot be observed, the library does not claim exact restoration.

### 1.2 Terminal-policy reset state

Some protocols define an explicit operation meaning “return control to terminal policy.” That is not the same as restoring an exact previous value.

Examples include:

- pointer-shape reset through empty OSC 22 payload;
- OSC 104 palette reset;
- OSC 110–114, 117, and 119 dynamic-color resets;
- synchronized-output final leave;
- terminal progress clear.

A reset command must never be described as exact restoration unless the protocol and owning API actually establish that guarantee.

### 1.3 Library-owned nested state without an observable external baseline

Some scoped operations can restore only state that `Icod.Terminal` itself created.

Examples include:

- OSC 8 hyperlink nesting;
- pointer-shape ownership.

The library can restore an outer Icod-owned state deterministically, but it does not claim to reconstruct arbitrary external state that existed before the first owner.

### 1.4 Ephemeral metadata

Some output changes terminal/application metadata but does not establish a restorable state machine owned by the library.

Examples include:

- title publication;
- current-location publication;
- OSC 133 prompt/command markers;
- notification publication;
- OSC 9;9 Windows current-directory compatibility.

These operations are not replayed automatically across lifecycle events and are not synthesized during disposal.

## 2. Presentation lease

`TerminalPresentationOptions` can request:

- alternate/full-screen presentation;
- keypad/application mode;
- cursor visibility.

At least one state must be requested.

`AcquirePresentationAsync(...)` returns a `TerminalPresentationLease` only after the requested transition succeeds.

### 2.1 Overlapping owners

Presentation leases may overlap.

The composition rules are:

- alternate-screen state remains active until the last requesting lease releases it;
- keypad/application mode remains active until the last requesting lease releases it;
- cursor visibility uses acquisition order, with the most recently acquired active cursor request controlling physical presentation;
- releasing the controlling cursor request restores the next-most-recent active request, or the terminal's ordinary cursor-visibility capability when no owner remains.

Lease disposal is asynchronous because release can require terminal output.

### 2.2 Failure and rollback

Presentation acquisition is transactional over the state transitions it performs.

If a later step fails after an earlier step succeeded, the manager attempts to roll back the successful progress. If transition and rollback both fail, both failures are surfaced and the manager does not retain a ghost public lease representing state it could not establish.

A later acquisition can recover only through the normal manager/state-validity path; the library does not silently advance believed state after a double failure.

### 2.3 Lifecycle and teardown barrier

New public presentation acquisition is rejected while the session is suspending, suspended, re-entering terminal state, or disposing.

Cleanup remains allowed. Existing lease disposal, rollback, lifecycle release/reentry, and final session restoration must not deadlock behind the acquisition barrier.

## 3. Presentation and modern keyboard composition

Kitty progressive keyboard state is screen-local.

When a managed presentation transition changes main/alternate screen while a modern keyboard request is active, `Icod.Terminal` coordinates the screen transition with input-protocol ownership conceptually as:

```text
pop current screen-local keyboard state
switch terminal screen
push desired keyboard state on the new screen
```

The whole handoff participates in the shared state-composition domain so a concurrent input-protocol acquisition/release cannot splice another keyboard transition between those steps.

If screen transition or keyboard handoff fails, rollback follows the same truthful-state rule: the library either restores the prior known state or surfaces uncertainty.

## 4. Cursor visibility vs cursor style vs pointer shape

These are three separate concepts:

- **cursor visibility** — whether the terminal text cursor is hidden/normal/very visible; owned by presentation leases;
- **cursor style** — block/underline/bar and blink policy; controlled through DECSCUSR APIs;
- **pointer shape** — graphical mouse-pointer shape; controlled through OSC 22.

No operation in one family implicitly changes or claims ownership of another family.

## 5. Cursor-style exact restoration

`AcquireCursorStyleAsync(...)` is an exact-restoration lease.

For the outermost owner:

1. the session queries the current semantic cursor style;
2. acquisition fails without mutation if a truthful baseline cannot be established;
3. the requested style is emitted;
4. nested owners use the currently known Icod-owned style;
5. inner release restores the immediately outer owned style;
6. final release restores the originally observed baseline.

Cursor-style nesting is strict LIFO. Out-of-order release fails without changing tracked or physical state.

A failed release retains cleanup responsibility so a later retry or final session disposal can attempt restoration again.

Managed suspend restores the observed baseline; resume reapplies the innermost remaining logical owner after session re-entry. Releasing an owner while suspended updates logical ownership without fabricating physical output.

The library does not use a guessed block cursor, DECSCUSR parameter `0`, or another reset-like value as a substitute for an observed baseline.

## 6. Synchronized output

`AcquireSynchronizedOutputAsync(...)` owns DEC private mode 2026 as a shared boolean state.

- first owner enters synchronized output;
- nested owners do not emit repeated enter frames;
- owners may release out of order because each requests the same boolean state;
- final owner leaves synchronized output and flushes;
- failed final release retains cleanup responsibility for retry;
- session disposal remains final cleanup authority.

Synchronized output is a terminal-side presentation-timing bracket. `Icod.Terminal` does not buffer application output into a private transaction and does not change the semantic meaning of writes made while the lease is active.

During managed suspend, physical synchronized-output state is left/neutralized and flushed; logical ownership survives. Resume re-enters only if an owner remains.

## 7. Terminal progress ownership

`AcquireProgressAsync(...)` owns terminal progress reporting through the supported OSC 9;4 semantic API.

Acquisition itself emits no progress value.

Owners are identity-aware and need not be released in LIFO order:

- a newer owner which has not reported does not mask a lower owner that has a visible report;
- updates by a non-controlling owner are logical-only;
- release of the controlling owner restores the newest remaining reported owner;
- final release clears library-owned progress;
- failed cleanup remains retryable.

Managed suspend clears physical progress while retaining logical ownership. Resume restores the controlling logical progress when one remains.

## 8. Pointer-shape ownership

OSC 22 pointer ownership differs from cursor-style exact restoration.

`AcquirePointerShapeAsync(...)` owns only Icod-created pointer state:

- the newest active owner controls physical pointer shape;
- owners may release out of order;
- non-controlling release is logical-only;
- controlling release restores the newest remaining Icod-owned shape;
- final release sends the protocol's terminal-policy reset;
- final release does **not** claim restoration of an unknown pre-Icod pointer shape.

`TerminalPointerShape.Default` means the CSS-compatible pointer shape named `default`; it is not a reset operation. `ResetPointerShapeAsync()` is the distinct terminal-policy reset.

Managed suspend resets physical pointer state to terminal policy and retains logical owners. Resume reapplies the newest surviving logical owner.

## 9. Palette and dynamic colors

Unscoped color setters, queries, and reset operations remain available, but scoped ownership has stronger rules.

### 9.1 Terminal-policy color resets

OSC 104 and OSC 110–114/117/119 mean terminal-policy reset. They do not mean “restore the exact color that existed before this process changed it.”

### 9.2 Exact-restoration color leases

`AcquirePaletteColorAsync(...)` and `AcquireDynamicColorAsync(...)` are query-before-mutate exact-restoration owners.

For the first owner of a color identity:

1. the current color is queried;
2. acquisition does not mutate if observation fails;
3. the requested owned color is emitted only after the baseline is known.

Nested owners are identity-aware and may be released out of order. Final release replays the exact observed color using the appropriate set form, not a terminal reset command.

A failed restoration keeps logical cleanup responsibility so `DisposeAsync()` can be retried and final session disposal can make another restoration attempt.

### 9.3 Invalidation and lifecycle epochs

`InvalidateState()` means physical color state can no longer be trusted; logical ownership remains.

Before managed suspend, scoped color ownership restores the current external baseline. That pre-suspend observation is not trusted after resume. During lifecycle re-entry, the session's internal observation phase queries a fresh external baseline before retained owned color is reapplied.

This is why scoped color ownership participates in the internal lifecycle query window documented in `Queries-and-Responses.md`.

### 9.4 Scoped and unscoped mutation

Unscoped set/reset operations are rejected while scoped ownership for the corresponding color family is active. Otherwise an unrelated mutation could silently invalidate the exact-restoration guarantee.

Observation queries remain allowed because observation does not mutate ownership.

## 10. Hyperlink ownership

OSC 8 hyperlink state is library-owned scoped state without an observable external baseline.

Hyperlink leases are strict LIFO:

```text
Acquire A -> begin A
Acquire B -> begin B
Dispose B -> re-emit A
Dispose A -> close hyperlink
```

The session restores only hyperlink state it created. It does not claim to discover or restore hyperlink state that predated Icod ownership.

Managed suspend emits a close while retaining the logical stack. After successful session re-entry, the innermost surviving hyperlink is re-emitted.

## 11. Invalidation

`TerminalSession.InvalidateState()` records that physical session-managed state is no longer trustworthy.

It is not a generic “reset everything” command. It does not automatically emit ephemeral metadata, clear application-defined terminal content, or invent restoration traffic for state the library never owned.

State managers recover according to their own truthful ownership contracts when a subsequent safe transition or lifecycle re-entry requires it.

## 12. Session disposal

`TerminalSession.DisposeAsync()` is the final cleanup/restoration authority for session-owned state.

Disposal:

- stops accepting new high-level session output/state acquisition;
- closes outstanding query transactions;
- stops lifecycle processing;
- releases/neutralizes input and presentation state;
- attempts cleanup/restoration for active scoped owners;
- flushes where required by the owning protocol;
- restores the captured native terminal baseline;
- preserves multiple cleanup failures rather than silently discarding them.

Outstanding lease objects become stale after owner-level cleanup and must not resurrect terminal state when later disposed.

## 13. Normative principle

A 1.x reversible-state API must say which of these it promises:

- exact restoration of an observed baseline;
- restoration of an outer Icod-owned state;
- terminal-policy reset;
- simple ephemeral emission with no restoration claim.

If a future feature cannot state that truthfully, it should not be exposed as a generic reversible lease merely for API symmetry.
