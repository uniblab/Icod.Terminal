# T113 — Session Pointer-Shape Manager and Nesting

**Release:** `0.11.0`  
**Tranche:** `T113`  
**Development version:** `0.11.0-alpha.4`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T113 adds session-owned OSC 22 pointer-shape state on top of the byte-exact writer and the frozen 30-value semantic model.

The manager provides ordered, identity-aware logical ownership without depending on Kitty's terminal-side pointer stack.

---

## 2. Acquisition

Each acquisition validates the semantic `TerminalPointerShape`, rejects redirected output, and emits the requested canonical OSC 22 shape through the existing session output-serialization domain.

The newest active owner controls physical pointer shape.

Acquisition does not flush output and successful completion proves protocol emission only.

---

## 3. Nested ownership

Owners are ordered by acquisition.

If owner A requests `Pointer` and owner B later requests `Wait`, physical state follows:

```text
pointer
wait
```

When B releases, A's semantic shape is restored:

```text
pointer
```

No terminal-side OSC 22 push/pop sequence is required.

---

## 4. Out-of-order release

Releasing a non-controlling owner removes only that logical owner and emits no pointer frame.

Releasing the controlling owner restores the newest remaining owner.

This makes ownership identity-aware and out-of-order-safe.

---

## 5. Final release

When the final owner releases, T113 emits the canonical empty-payload OSC 22 reset:

```text
ESC ] 22 ; ESC \
```

This returns pointer control to terminal policy.

It does **not** claim restoration of an arbitrary pre-existing external pointer shape because portable base OSC 22 provides no truthful baseline query for that state.

The CSS semantic `TerminalPointerShape.Default` remains distinct and emits `default` rather than the empty reset payload.

---

## 6. Lifecycle

The pointer manager participates in core terminal-session lifecycle.

- suspension resets physical pointer state while retaining logical owners;
- releasing owners while suspended is logical-only;
- re-entry restores the newest remaining owner;
- releasing every owner while suspended prevents re-entry;
- `TerminalSession.InvalidateState()` invalidates pointer physical-state knowledge;
- session disposal performs an authoritative reset before synchronized-output final cleanup.

---

## 7. Failure and cleanup debt

Failed physical transitions mark pointer state uncertain and retain cleanup responsibility.

Cleanup debt or invalidated state blocks new scoped acquisition until recovery.

Controlling release failure leaves the owner logically present so later cleanup can retry.

Cleanup and restoration writes are non-caller-cancellable.

T115 will add focused failure-injection and retry tests for these transitions.

---

## 8. Output serialization

Pointer ownership does not retain the session output gate for lease lifetime.

Each transition acquires only the existing session/control output serialization needed for that single physical state change.

No new transport or lock domain is introduced.

---

## 9. Tests

T113 tests cover:

- first-owner emission;
- nested controlling-owner restoration;
- out-of-order non-controlling release silence;
- final terminal-policy reset;
- suspend/reset and resume/restore;
- release-all-while-suspended preventing re-entry;
- redirected-output rejection without emission;
- session-disposal reset.

---

## 10. Decision

T113 establishes the internal session-owned pointer-shape state machine required for the public lease and setter APIs in T114.

No raw OSC 22 names or Kitty terminal-side stack operations are exposed.
