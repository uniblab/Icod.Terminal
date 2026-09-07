# T173 — Negotiated Kitty Keyboard Ownership

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T173  
**Version:** `0.17.0-alpha.4`  
**Status:** Complete and green at workflow #799

## Purpose

T173 makes the semantic keyboard reporting modes frozen in T170 requestable through the existing reversible rich-input lease architecture. It does not add a second keyboard lease, a terminal-brand detector, or raw Kitty control-sequence APIs.

## Public lease surface

`TerminalInputProtocolOptions` now includes:

```csharp
public TerminalKeyboardReportingMode? KeyboardReportingMode { get; init; }
```

`TerminalInputProtocolLease` exposes the corresponding owned request:

```csharp
public TerminalKeyboardReportingMode? KeyboardReportingMode { get; }
```

A null value means that lease does not request negotiated keyboard reporting.

## Support detection

Keyboard acquisition is gated by the Kitty progressive-keyboard support procedure.

The session emits the combined bounded probe:

```text
CSI ? u
CSI c
```

The input decoder temporarily observes an optional Kitty flags response:

```text
CSI ? <flags> u
```

while the existing query transaction owns the following Primary Device Attributes response as the deterministic delimiter.

Consequences:

- no `$TERM`/brand heuristic;
- no arbitrary sleep to decide support;
- coalesced flags + DA replies are supported;
- fragmented flags and DA replies are supported;
- DA without a preceding flags report yields a controlled unavailable keyboard lease;
- the scoped flags observer is removed after the probe and does not become a permanent application-input filter.

## Physical Kitty ownership

The input-protocol manager owns at most one physical Kitty stack entry for its current managed screen.

Reporting modes map exactly to:

```text
Disambiguated -> CSI > 5 u
EventTypes    -> CSI > 7 u
AllKeys       -> CSI > 31 u
```

Final release uses:

```text
CSI < u
```

for one library-owned pop.

A change from one active reporting strength to another is represented as a pop of the current library entry followed by a push of the replacement entry.

## Strongest active request

Overlapping leases reconcile according to:

```text
none < Disambiguated < EventTypes < AllKeys
```

A weaker lease added beneath a stronger active lease produces no keyboard transition. Releasing the strongest lease restores the strongest remaining request deterministically.

Logical lease count is independent of Kitty terminal stack depth.

## Composition

Keyboard reporting is part of the same `TerminalInputProtocolManager` desired/applied state as:

- bracketed paste;
- focus reporting;
- mouse tracking.

Existing historical ordering inside paste/focus/mouse transitions remains unchanged. Keyboard pop is performed before other downgrades and keyboard push after other upgrades, allowing the existing transactional rollback structure to reverse the transition deterministically.

## Failure and cancellation

The existing transition contract is retained:

- cancellation before acquisition/commitment emits nothing;
- support-probe failure does not create a lease;
- a failed transition removes the new lease and invalidates believed physical state;
- rollback is attempted through the existing transactional manager;
- committed transition writes remain serialized through session control output;
- disposal restores owned keyboard state using non-cancellable cleanup semantics.

## Deliberately unchanged xterm behavior

T173 sends no xterm `modifyOtherKeys` enable/disable sequence. xterm remains decode-only compatibility for T174.

## Cross-manager lifecycle obligations carried to T175

Two T170 release invariants intentionally remain release gates rather than being forced into the basic ownership tranche:

1. managed main/alternate-screen handoff must use keyboard pop → screen switch → keyboard push so an owned Kitty stack entry is never stranded on the inactive screen;
2. lifecycle resume must re-establish support/state truthfully after terminal state may have changed.

These are cross-manager/query lifecycle operations with lock-order and rollback implications. They are assigned explicitly to T175 composition/hardening, together with fault injection and deadlock testing. They remain mandatory for 0.17 stable closure.

T173 itself establishes and validates the normal live-session negotiation/ownership primitive on which that hardening is based.

## Validation

Focused tests cover:

- coalesced support response + DA delimiter;
- fragmented support response and delimiter;
- unsupported DA-only response;
- exact 5/7/31 pushes;
- exact one-entry pop;
- nested upgrade and downgrade reconciliation;
- keyboard lease property values;
- composition with bracketed paste, focus and mouse reporting.

The exact `0.17.0-alpha.4` head passed Windows/Linux/macOS PR validation at workflow #799, including retained historical package/downstream gates.

Next: T174 — xterm `modifyOtherKeys` decoder compatibility.
