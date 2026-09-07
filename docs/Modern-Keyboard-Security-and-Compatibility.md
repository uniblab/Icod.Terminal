# Modern Keyboard Security and Compatibility

This document is the permanent 1.x companion for negotiated modern keyboard reporting. The general event model is defined in `Input-and-Events.md`; this document concentrates on compatibility, reversible activation, and the additional data exposed by modern keyboard protocols.

## Negotiation and ownership

Kitty progressive keyboard reporting is the modern keyboard protocol that `Icod.Terminal` actively negotiates and lease-owns.

Applications request semantic reporting intensity through:

```csharp
TerminalInputProtocolOptions.KeyboardReportingMode
```

using `Disambiguated`, `EventTypes`, or `AllKeys`.

The library queries support before acquisition and uses reversible push/pop ownership. It does not treat terminal brand, `TERM`, or environment variables as proof that activation is safe.

xterm `modifyOtherKeys` is decode-only compatibility. `Icod.Terminal` does not blindly enable or disable it because arbitrary pre-existing xterm state cannot be restored with sufficient confidence.

## No raw vendor control surface

The public API does not expose:

- raw Kitty push/pop helpers;
- generic Kitty flag integers;
- raw Kitty private-use key codes;
- generic keyboard CSI writers;
- xterm `modifyOtherKeys` mutation helpers;
- automatic terminal-brand activation.

The public contract is the semantic reporting request and the normalized `TerminalInputEvent` model.

## Semantic event model

Modern key events can report more information than traditional terminal key sequences:

- press, repeat, or release phase;
- expanded modifiers;
- shifted character identity;
- base-layout character identity;
- associated text;
- functional key identities beyond the traditional terminfo set.

Known functional identities map to `TerminalKey`. A syntactically valid modern functional identity that this library version does not recognize maps to `TerminalKey.Unrecognized`; raw vendor/private-use integers do not become the public key identity.

Traditional keyboard decoding remains active regardless of whether a modern protocol is negotiated. Applications should treat modern reporting as an optional enhancement, not a prerequisite for ordinary input.

## Bounded decoding

Modern keyboard frames use the same bounded incremental parser as other terminal input.

The parser:

- accepts fragmented and coalesced transport reads;
- preserves partial UTF-8/control-sequence state across caller wait timeout/cancellation;
- bounds undecoded input;
- consumes or rejects malformed modern frames without indefinite accumulation;
- resumes ordinary input decoding after recoverable malformed input;
- routes active terminal-query responses before exposing the same bytes as ordinary input.

Modern keyboard support does not create a second input stream or parser.

## Screen-local state

Kitty keyboard stack entries are screen-local.

Managed main/alternate-screen transitions therefore hand library-owned keyboard state across the screen boundary conceptually as:

```text
pop requested keyboard state
switch screen
push requested keyboard state
```

This operation is serialized with presentation and input-protocol mutation so another lease cannot splice a push/pop into the middle of the handoff.

If the transition or rollback cannot establish trustworthy state, the failure is surfaced and believed state is invalidated rather than silently advanced.

## Lifecycle recovery

Managed suspend releases library-owned keyboard state before terminal ownership is handed away.

Managed resume does not assume that support observed before suspend is still true. The session re-establishes support through its lifecycle observation/query path before pushing a requested mode again.

That lifecycle observation uses the same query ambiguity domain as ordinary queries, so a late pre-suspend response cannot be mistaken for a post-resume capability result.

If support cannot be re-established, the manager fails closed rather than blindly restoring the previously believed protocol state.

## Overlapping leases

Modern keyboard reporting participates in the general input-protocol lease model.

Multiple leases may request different semantic intensities. The strongest active request is applied. Releasing a stronger lease restores the strongest remaining request rather than disabling reporting unconditionally.

Session disposal remains authoritative cleanup; stale lease disposal after owner cleanup must not reactivate keyboard state.

## Data exposure

Modern keyboard metadata can reveal more user-input context than traditional terminal key sequences.

In particular:

- `AssociatedText` may contain text produced by a key event;
- shifted/base-layout identities may reveal alternate keyboard-layout information;
- explicit release/repeat phases reveal more timing/state detail;
- expanded modifiers reveal more chord context.

Applications should avoid logging, persisting, transmitting, or displaying these fields unless needed for their purpose.

`Icod.Terminal` does not automatically redact application-requested input metadata.

## Compatibility scope

The 1.x contract guarantees the semantic interpretation and ownership rules implemented by the frozen public surface. It does not promise:

- support for every future Kitty extension;
- exposure of arbitrary future Kitty private-use assignments;
- mutation of xterm `modifyOtherKeys` state;
- IME protocols;
- scan-code protocols;
- global hotkeys or OS keyboard hooks;
- keyboard remapping/binding policy.

Future modern-keyboard additions must preserve bounded parsing, semantic normalization, one authoritative reader, and reversible/truthful state ownership.
