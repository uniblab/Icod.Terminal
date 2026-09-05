# Icod.Terminal 0.8 Public API Baseline

**Release:** `0.8.0`  
**Theme:** typed cursor-style control, observation, and truthful restoration  
**Status:** Frozen for stable release

## Public delta

The reviewed 0.8 public API adds one enum, two public state objects, and three `TerminalSession` operations.

```csharp
public enum TerminalCursorStyle {
	BlinkingBlock,
	SteadyBlock,
	BlinkingUnderline,
	SteadyUnderline,
	BlinkingBar,
	SteadyBar
}

public sealed class TerminalCursorStyleObservation {
	public bool IsSupported { get; }
	public TerminalCursorStyle? Style { get; }
}

public sealed class TerminalCursorStyleLease : IAsyncDisposable {
	public TerminalCursorStyle Style { get; }
	public ValueTask DisposeAsync();
}

public ValueTask SetCursorStyleAsync(
	TerminalCursorStyle style,
	CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleLease> AcquireCursorStyleAsync(
	TerminalCursorStyle style,
	TimeSpan timeout,
	CancellationToken cancellationToken = default
);
```

## Wire mapping

The semantic values map deterministically to DECSCUSR:

```text
BlinkingBlock     -> CSI 1 SP q
SteadyBlock       -> CSI 2 SP q
BlinkingUnderline -> CSI 3 SP q
SteadyUnderline   -> CSI 4 SP q
BlinkingBar       -> CSI 5 SP q
SteadyBar         -> CSI 6 SP q
```

Outbound frames use canonical seven-bit CSI (`ESC [`). Values 5 and 6 are the xterm-compatible bar-cursor extension retained by the 0.8 contract.

Inbound DECRQSS cursor-style status accepts omitted, `0`, and `1` as `BlinkingBlock`; recognized values `2` through `6` map to the corresponding semantic style. Unknown positive values, including xterm parameter `7`, are not silently coerced and fail with `FormatException`.

## Setter contract

`SetCursorStyleAsync(...)` is explicit semantic output. Arguments are validated before emission; known redirected output is rejected; the operation participates in the session-owned output gate; and it does not implicitly flush.

Successful completion proves only that the complete DECSCUSR frame was emitted. It does not prove that the terminal recognized or applied the requested cursor style. In particular, choosing a bar style does not manufacture proof that the current emulator implements the xterm extension.

Unscoped cursor-style mutation is rejected while a cursor-style lease is active. This prevents an unrelated mutation from invalidating the lease's exact restoration model.

## Observation contract

`QueryCursorStyleAsync(...)` is an explicit active query built on the existing DECRQSS `SP q` transaction path. Every call has a caller-visible timeout.

A positive recognized response returns:

```text
IsSupported = true
Style       = recognized semantic style
```

An explicit negative DECRQSS response returns:

```text
IsSupported = false
Style       = null
```

Malformed or unrecognized positive state fails with `FormatException`; caller cancellation remains cancellation; and deadline expiration remains `TimeoutException`. A timeout does not prove lack of support.

Opening, suspending, resuming, or disposing a session does not automatically probe cursor style. No support flag is cached from `TERM`, terminal identity, or environment variables.

## Lease and restoration contract

`AcquireCursorStyleAsync(...)` is the only 0.8 API which claims exact restoration.

For the outermost lease, the session first observes the actual current semantic cursor style. The requested style is not emitted unless that observation succeeds. An explicit unsupported observation therefore causes `NotSupportedException` with no cursor-style mutation.

Nested leases reuse the known session-owned top style and do not issue redundant queries. Leases are strict LIFO:

```text
observe baseline B
Acquire A -> emit A
Acquire C -> emit C
Dispose C -> restore A
Dispose A -> restore B
```

Out-of-order release fails without changing tracked or physical state. A failed release retains restoration ownership for retry or session-disposal cleanup.

If the requested DECSCUSR write reports a transport failure after emission has been attempted, acquisition performs a best-effort write of the known prior semantic style before propagating the acquisition failure. If that recovery write also fails, both failures are reported and an outermost known baseline remains owned by the session for final disposal retry. Cancellation that occurs before the requested style write commits still causes no cursor-style mutation.

Before managed suspension, an active cursor-style lease restores the originally observed baseline. After successful session re-entry, the innermost active logical style is re-applied. Releasing a lease while suspended updates logical ownership without emitting an extra cursor-style frame. Session disposal performs final best-effort restoration of the observed baseline and invalidates outstanding lease objects.

Exact restoration never means emitting a guessed reset. The implementation does not use DECSCUSR parameter `0`, a hard-coded block cursor, or xterm parameter `7` as a substitute for an observed prior state.

## Cursor visibility remains independent

`TerminalCursorStyle` and `TerminalCursorVisibility` are deliberately separate concepts.

Cursor visibility continues to belong to reversible `TerminalPresentationLease` state. Changing cursor style does not show or hide the cursor, and changing cursor visibility does not select a cursor shape or blink policy. Both mechanisms serialize through the same session-owned output channel without conflating their ownership models.

## Deliberate omissions

The 0.8 public API does not expose:

- raw DECSCUSR numeric parameters;
- a public generic CSI writer;
- arbitrary CSI parameter/intermediate/final byte construction;
- a `Default`, `Reset`, or `Initial` cursor style;
- xterm parameter `7` as a semantic style;
- cursor color control;
- automatic cursor-style support detection;
- cursor-style probing during session open;
- a cached terminal-emulator capability flag;
- OS-native pointer or mouse-cursor APIs;
- synchronized output mode.

Synchronized output remains a separate 0.9 design problem.

## Regret audit

The 0.8 surface passes the stable-release regret audit because:

1. the public enum is semantic and closed rather than a thin raw-number wrapper;
2. protocol-specific CSI construction remains internal;
3. support uncertainty is explicit instead of inferred from terminal identity;
4. setter completion is correctly described as emission, not support proof;
5. query timeout, unsupported response, malformed response, and cancellation remain distinct outcomes;
6. exact restoration is offered only when prior state was actually observed;
7. nested ownership is deterministic and strict LIFO;
8. failed acquisition does not silently abandon a known restoration baseline;
9. cursor shape and cursor visibility remain independent public concepts;
10. the implementation reuses the existing single-reader query architecture and shared output gate;
11. no 0.9 synchronized-output policy is prematurely frozen into the 0.8 API.

No further public surface is required for `0.8.0`.
