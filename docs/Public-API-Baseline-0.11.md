# Icod.Terminal 0.11 Public API Baseline

**Release:** `0.11.0`  
**Theme:** OSC 22 terminal mouse-pointer shape control  
**Status:** Reviewed public surface; stable closure pending

---

## Public API delta from 0.10

`0.11.0` adds one semantic pointer-shape enum, one scoped owner, one observation type, explicit pointer mutation, and explicit bounded Kitty-compatible OSC 22 queries.

```csharp
namespace Icod.Terminal;

public enum TerminalPointerShape {
	Alias,
	Cell,
	Copy,
	Crosshair,
	Default,
	EastResize,
	EastWestResize,
	Grab,
	Grabbing,
	Help,
	Move,
	NorthResize,
	NorthEastResize,
	NorthEastSouthWestResize,
	NoDrop,
	NotAllowed,
	NorthSouthResize,
	NorthWestResize,
	NorthWestSouthEastResize,
	Pointer,
	Progress,
	SouthResize,
	SouthEastResize,
	SouthWestResize,
	Text,
	VerticalText,
	WestResize,
	Wait,
	ZoomIn,
	ZoomOut
}

public sealed class TerminalPointerShapeLease : IAsyncDisposable {
	public TerminalPointerShape Shape { get; }
	public ValueTask DisposeAsync();
}

public sealed class TerminalPointerShapeObservation {
	public bool HasShape { get; }
	public TerminalPointerShape? Shape { get; }
}

public sealed partial class TerminalSession {
	public ValueTask SetPointerShapeAsync(
		TerminalPointerShape shape,
		CancellationToken cancellationToken = default
	);

	public ValueTask ResetPointerShapeAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalPointerShapeLease> AcquirePointerShapeAsync(
		TerminalPointerShape shape,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalPointerShapeObservation> QueryCurrentPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalPointerShapeObservation> QueryDefaultPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalPointerShapeObservation> QueryGrabbedPointerShapeAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	);

	public ValueTask<bool> QueryPointerShapeSupportAsync(
		TerminalPointerShape shape,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	);
}
```

---

## Semantic shape contract

The 30 public values map one-to-one to the frozen CSS-compatible OSC 22 names documented in T110/T112.

`TerminalPointerShape.Default` requests the CSS pointer shape named `default`. It is not a terminal-policy reset.

A terminal-policy reset is an explicit separate operation and emits an empty OSC 22 payload.

No public arbitrary pointer-name string is accepted.

---

## Explicit mutation contract

`SetPointerShapeAsync(...)` requests one semantic pointer shape without creating scoped ownership.

`ResetPointerShapeAsync(...)` releases the application's explicit OSC 22 pointer request back to terminal policy.

Unscoped mutation is rejected while a scoped pointer owner is active.

Successful completion proves complete protocol emission, not emulator recognition or visual application. Neither operation performs automatic support probing or an implicit flush.

---

## Scoped ownership contract

`AcquirePointerShapeAsync(...)` immediately emits the requested shape and returns an identity-aware owner.

- newest active owner controls physical pointer state;
- nested owners may be disposed out of order;
- non-controlling release is logical-only;
- controlling release restores the newest remaining Icod-owned shape;
- final release resets to terminal policy;
- final release does not claim restoration of an unknown pre-lease external pointer shape;
- successful repeated disposal is idempotent;
- failed restoration/reset retains ownership so disposal may retry.

The lease implementation does not depend on Kitty's terminal-side pointer stack.

---

## Query contract

The explicit query surface implements the reviewed Kitty-compatible OSC 22 query subset:

- current application pointer shape;
- terminal default pointer shape;
- grabbed pointer shape;
- support for one semantic shape.

Queries reuse the existing session-owned active-query transaction manager and response router. They have caller-visible finite deadlines and are never issued automatically during open, mutation, lease acquisition, lifecycle handling, or disposal.

A current-shape response of `0` is represented by `TerminalPointerShapeObservation.HasShape == false` and `Shape == null`. This is distinct from `TerminalPointerShape.Default`.

A support query accepts only explicit `0` or `1` replies. Timeout remains `TimeoutException`; it is not interpreted as proof that OSC 22 is unsupported.

---

## Lifecycle and failure contract

Scoped pointer ownership participates in managed suspend/resume and `TerminalSession.InvalidateState()`.

- suspend resets physical pointer state to terminal policy while retaining logical owners;
- resume restores the newest remaining logical owner;
- releasing all owners while suspended prevents re-entry;
- invalidation recovery re-establishes current logical state before the next semantic transition;
- failed acquisition/mutation performs best-effort recovery;
- failed controlling release remains retryable through the same lease;
- session disposal performs authoritative pointer reset;
- cleanup/recovery writes are not caller-cancellable and do not implicitly flush.

---

## Deliberate omissions

`0.11.0` does not add:

- public arbitrary OSC 22 strings;
- public generic OSC construction;
- arbitrary X11 cursor-name injection;
- Kitty pointer-stack push/pop APIs;
- automatic OSC 22 support probing;
- support inference from operating system, `TERM`, emulator identity, or environment variables;
- platform GUI pointer APIs;
- any conflation of OSC 22 mouse-pointer shape with DECSCUSR text-cursor style or cursor visibility.

Those omissions are part of the reviewed 0.11 contract rather than missing implementation.
