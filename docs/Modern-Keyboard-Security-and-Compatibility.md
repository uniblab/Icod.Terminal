# Modern Keyboard Security and Compatibility

`Icod.Terminal 0.17.0` adds modern keyboard support conservatively. The release treats terminal keyboard protocols as negotiated, stateful terminal features rather than as raw escape-sequence conveniences.

## Negotiation and ownership

Kitty progressive keyboard reporting is the only modern keyboard protocol that 0.17 actively negotiates and lease-owns.

The library queries support before acquisition and uses reversible push/pop ownership. It never assumes that a terminal brand or environment variable implies protocol support.

xterm `modifyOtherKeys` is decode-only compatibility. `Icod.Terminal` does not blindly enable or disable it because arbitrary pre-existing xterm state cannot be restored with sufficient confidence.

## No raw vendor control surface

The public API does not expose:

- raw Kitty push/pop helpers;
- generic Kitty flag integers;
- raw Kitty private-use key codes;
- generic keyboard CSI writers;
- xterm `modifyOtherKeys` mutation helpers;
- automatic terminal-brand activation.

Callers request semantic reporting modes through `TerminalInputProtocolOptions` instead.

## Bounded decoding

Modern keyboard decoding shares the existing bounded incremental terminal-input parser.

The parser:

- accepts fragmented and coalesced reads;
- bounds buffered input;
- consumes malformed modern frames without indefinite accumulation;
- resumes ordinary input decoding after malformed input;
- correlates active terminal-query responses before ordinary input events.

Unknown Kitty private-use functional codes are represented as `TerminalKey.Unrecognized`; raw PUA integers do not escape into the public API.

## Screen-local state

Kitty keyboard stack entries are screen-local. Managed main/alternate-screen transitions therefore move library ownership using:

```text
Kitty pop
screen switch
Kitty push
```

The operation is serialized against input-lease mutation so another request cannot push or pop keyboard state in the middle of the handoff.

Failure and rollback are explicit. The library does not silently claim a screen-local keyboard state that it could not establish.

## Lifecycle recovery

Managed suspend restores owned keyboard state before control is handed away.

Managed resume re-detects Kitty support before a requested mode is pushed again. If support cannot be re-established, the manager fails closed and invalidates its believed state rather than assuming the old capability still applies.

## Traditional fallback

Traditional keyboard parsing remains enabled regardless of modern negotiation. Applications should treat modern keyboard reporting as an optional enhancement, not a requirement for receiving ordinary keyboard input.

The bundled rich-input sample demonstrates this pattern by attempting Kitty `AllKeys` first and retaining a traditional-keyboard path when negotiation is unavailable.

## Data exposure

Associated text and alternate key identities come from the terminal protocol and may reveal more keyboard context than a traditional key event. Applications should avoid logging or transmitting these fields unless they actually need them.

The library does not automatically redact or suppress key-event metadata.

## Compatibility scope

The 0.17 stable contract guarantees semantic interpretation of the frozen Kitty/xterm forms documented by the release. It does not promise support for every vendor extension, future Kitty private-use assignment, IME protocol, scan-code protocol, global hotkey mechanism, or OS-level keyboard hook.

New protocol forms should be added only when they can preserve bounded parsing, truthful semantics, and reversible state ownership.
