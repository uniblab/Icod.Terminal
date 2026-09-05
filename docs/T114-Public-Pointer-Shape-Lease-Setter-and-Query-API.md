# T114 — Public Pointer-Shape Lease, Setter, and Query API

**Release:** `0.11.0`  
**Tranche:** `T114`  
**Development version:** `0.11.0-alpha.5`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T114 exposes the frozen OSC 22 semantic model through public `TerminalSession` operations without exposing raw OSC strings or terminal-specific X11 cursor names.

---

## 2. Public surface

T114 adds:

```csharp
public sealed class TerminalPointerShapeLease : IAsyncDisposable {
	public TerminalPointerShape Shape { get; }
	public ValueTask DisposeAsync();
}

public sealed class TerminalPointerShapeObservation {
	public bool HasShape { get; }
	public TerminalPointerShape? Shape { get; }
}

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
```

---

## 3. Explicit setter/reset semantics

`SetPointerShapeAsync(...)` emits one canonical OSC 22 set frame and creates no scoped logical owner.

`ResetPointerShapeAsync(...)` emits the empty OSC 22 terminal-policy reset and is distinct from requesting `TerminalPointerShape.Default`.

Unscoped set/reset operations are rejected while a scoped pointer-shape owner is active. They are also rejected while pointer-state cleanup or recovery remains pending.

Neither operation performs automatic support probing or implicit flushing.

---

## 4. Scoped lease semantics

`AcquirePointerShapeAsync(...)` immediately establishes one logical owner and emits the requested semantic pointer shape.

Leases inherit the T113 identity-aware rules:

- newest active owner controls physical pointer shape;
- non-controlling release is silent;
- controlling release restores the newest remaining Icod-owned shape;
- final release emits terminal-policy reset;
- successful repeated disposal is idempotent;
- failed restoration/reset retains ownership so disposal may retry.

No Kitty terminal-side push/pop stack is required by the lease implementation.

---

## 5. Explicit Kitty-compatible queries

T114 adds explicit queries for:

- `?__current__`;
- `?__default__`;
- `?__grabbed__`;
- one semantic pointer-shape support query.

Queries use the existing session-owned active-query transaction manager and response router. They are not issued automatically during session open, setter calls, lease acquisition, lifecycle transitions, or disposal.

The canonical query form is:

```text
ESC ] 22 ; ?query ESC \
```

The terminal reply is an ordinary OSC 22 response.

A current-shape reply of `0` means no application pointer shape is currently set. `TerminalPointerShapeObservation.HasShape` is therefore false and `Shape` is null. This remains distinct from the semantic CSS `default` shape.

A single-shape support query accepts only explicit `0` or `1` replies.

Timeout is not interpreted as proof that OSC 22 is unsupported.

---

## 6. Response framing and parsing

The OSC 22 response path accepts:

- seven-bit OSC with ST termination;
- seven-bit OSC with BEL termination;
- eight-bit OSC with C1 ST termination.

Correlated malformed OSC 22 responses are routed to the active transaction and fail with `FormatException` rather than being guessed or leaked into ordinary input.

Semantic shape replies must match the frozen 30-name canonical vocabulary exactly.

---

## 7. Tests

T114 adds public API tests proving:

- CSS `default` set versus terminal-policy reset;
- nested public leases and restoration;
- out-of-order-safe lease disposal;
- unscoped mutation rejection while leases are active;
- idempotent successful lease disposal.

Active-query tests prove:

- byte-exact current/default/grabbed query requests;
- semantic response decoding;
- explicit zero current-state handling;
- single-shape support 0/1 replies;
- BEL and C1 response framing;
- malformed correlated response failure;
- pre-cancelled query no-emission behavior.

---

## 8. Decision

T114 freezes a semantic public OSC 22 API around explicit mutation, scoped ownership, and explicit bounded observation. No raw OSC 22 strings, X11 cursor aliases, automatic probing, or Kitty push/pop public API is introduced.

T115 may now harden lifecycle, invalidation, failed acquisition/release recovery, and session-disposal failure behavior.
