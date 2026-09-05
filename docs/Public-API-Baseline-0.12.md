# Icod.Terminal 0.12 Public API Baseline

**Release:** `0.12.0`  
**Theme:** semantic OSC 133 shell-integration / semantic-prompt markers  
**Status:** Frozen stable public surface

---

## Public API delta from 0.11

`0.12.0` adds exactly five semantic operations to `TerminalSession`:

```csharp
namespace Icod.Terminal;

public sealed partial class TerminalSession {
	public ValueTask BeginPromptAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask BeginCommandInputAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask BeginCommandOutputAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask FinishCommandAsync(
		byte exitStatus,
		CancellationToken cancellationToken = default
	);

	public ValueTask AbortCommandAsync(
		CancellationToken cancellationToken = default
	);
}
```

No new public marker enum, payload type, command-region lease, or arbitrary OSC API is part of the 0.12 surface.

---

## Portable wire contract

The five operations map to the portable FinalTerm/iTerm2-style OSC 133 core:

```text
BeginPromptAsync()        -> OSC 133 ; A ST
BeginCommandInputAsync()  -> OSC 133 ; B ST
BeginCommandOutputAsync() -> OSC 133 ; C ST
FinishCommandAsync(n)     -> OSC 133 ; D ; n ST
AbortCommandAsync()       -> OSC 133 ; D ST
```

Outbound OSC 133 frames use canonical ST termination (`ESC \\`).

`FinishCommandAsync(...)` accepts `byte`, so the public completion-status domain is exactly `0..255`. `FinishCommandAsync(0)` is successful completion with explicit status zero and is semantically distinct from `AbortCommandAsync()`, which carries no exit status.

---

## Independent-call contract

All five operations are independently callable.

`Icod.Terminal` does not maintain a synthetic A -> B -> C -> D shell-history state machine. It therefore does not reject a marker merely because earlier markers were not observed through the same session.

This is intentional for prompt redraws, attaching integration in the middle of an interaction, interruption recovery, multiplexers, subshells, nested REPLs, and other environments where the library is not the authority for shell history.

Callers that know the canonical command lifecycle should normally emit A, B, C, then either `D;status` or bare D. That ordering is application policy, not retained `TerminalSession` state.

---

## Output and cancellation contract

Every semantic marker participates in the same session-owned output-serialization domain as application text and existing terminal-control output.

- known redirected output is rejected;
- caller cancellation is observed before output commitment;
- cancellation while queued for the session output gate emits nothing;
- the complete frame is constructed before commit;
- once committed, the frame is written with `CancellationToken.None`;
- marker methods do not flush implicitly;
- successful completion proves complete emission only, not terminal recognition.

---

## Lifecycle and failure contract

OSC 133 markers are transient annotations, not library-owned terminal modes.

Therefore 0.12 adds no OSC 133 lifecycle participant or cleanup owner:

- suspend emits no automatic marker;
- resume replays no marker;
- disposal emits no automatic finish or abort marker;
- repeated disposal has no OSC 133 cleanup side effect;
- failed committed writes propagate without compensating markers;
- a failed marker does not fabricate command history or advance retained semantic state;
- later independent marker calls remain available when the session itself remains usable.

---

## Composition contract

OSC 133 composes through the existing output domain with:

- ordinary application text;
- OSC 0/1/2 title operations;
- OSC 7 current-location publication;
- OSC 8 hyperlinks;
- OSC 9;4 progress;
- OSC 22 pointer shape;
- OSC 52 clipboard operations;
- DECSCUSR cursor style;
- reversible presentation state;
- reversible input-protocol leases;
- DEC private mode 2026 synchronized output;
- active terminal queries;
- downstream `Icod.DCurses` refresh output.

OSC 133 does not acquire, restore, invalidate, or reinterpret any of those subsystems.

---

## Support posture

The API is emission-oriented. A successful method call means the complete OSC 133 frame was written to the interactive session output.

It does not prove that the terminal recognizes OSC 133 or uses the marker to annotate scrollback. `Icod.Terminal` does not infer support from operating system, `TERM`, emulator identity, environment variables, or successful emission, and 0.12 performs no automatic support probe.

---

## Deliberate omissions

`0.12.0` does not add:

- public raw A/B/C/D marker characters;
- a public generic `WriteOsc133Async(...)` escape hatch;
- arbitrary key/value OSC 133 metadata;
- command text publication;
- nullable completion status that conflates abort with completion;
- a scoped command-region lease;
- retained shell history or command-region state;
- automatic shell detection or shell-startup-file modification;
- Kitty-only OSC 133 metadata as portable core behavior;
- VS Code OSC 633 or iTerm2 OSC 1337 as though either were OSC 133.

These omissions are part of the reviewed stable contract.
