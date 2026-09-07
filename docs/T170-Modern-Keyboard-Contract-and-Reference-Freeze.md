# T170 — Modern Keyboard Contract and Reference Freeze

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T170  
**Version:** `0.17.0-alpha.1`  
**Status:** Frozen; implementation begins at T171

## 1. Purpose

Traditional terminal keyboard encoding is inherently ambiguous. Distinct key combinations can produce the same bytes, modern modifiers are poorly represented, and traditional input has no reliable key-release/repeat distinction.

0.17 addresses this by adding an explicit modern keyboard semantic layer and negotiated protocol support while preserving the existing traditional decoder as the default compatibility baseline.

The release does **not** expose arbitrary keyboard escape sequences. Applications request semantic keyboard capabilities; `Icod.Terminal` owns supported protocol negotiation, decoding, nesting, lifecycle restoration, and fallback behavior.

## 2. Existing compatibility baseline

Before 0.17 the public model provides:

- `TerminalInputEventKind.Text` for ordinary Unicode text;
- `TerminalInputEventKind.Key` for named or modified key events;
- `TerminalKey` identities for Character, Enter, Space, Escape, Backspace, Tab, arrows, Home/End, PageUp/PageDown, Insert/Delete, and numbered function keys;
- `TerminalKeyModifiers.Shift = 1`, `Control = 2`, and `Alt = 4`;
- no public press/repeat/release distinction;
- no Super/Hyper/Meta/lock modifier flags;
- reversible rich-input ownership for paste/focus/mouse through `TerminalInputProtocolOptions` / `TerminalInputProtocolLease`.

These existing values and traditional decoding semantics are compatibility constraints. They are not renumbered or repurposed.

## 3. Protocol tiers

### 3.1 Kitty comprehensive keyboard protocol — negotiated tier

Kitty progressive keyboard enhancement is the only keyboard mode that 0.17 will actively acquire through `TerminalInputProtocolManager`.

Reasons:

- applications can explicitly query support;
- applications can push and pop keyboard-mode state;
- terminals maintain separate keyboard-mode stacks for main and alternate screens;
- the protocol reports richer modifiers and optional event types;
- progressive flags allow the library to request a precise semantic contract.

There is no terminal-brand heuristic.

### 3.2 xterm `modifyOtherKeys` — decoder compatibility only

0.17 will decode the supported xterm `modifyOtherKeys` forms when they arrive, but it will **not** enable or disable `modifyOtherKeys` from the reversible input-protocol lease.

This is a deliberate correction to the initial 0.17 roadmap. xterm `modifyOtherKeys` does not provide a robust query/push/pop state model that lets `Icod.Terminal` prove restoration of pre-existing application state. Sending a blind enable and later disable would violate the repository's reversible-ownership rules.

T174 is therefore a decoder/interoperability tranche, not a negotiated-ownership tranche.

Supported xterm compatibility syntax will include the conventional level-2 modified ordinary-key representation and the CSI-u formatting form when unambiguous. Both normalize to the same semantic key model and always report `Press` because xterm does not provide Kitty's event-type contract.

### 3.3 Plain fixterms/CSI-u

A globally configured fixterms/CSI-u mode is not a lease-owned protocol in 0.17 because applications cannot reliably request, detect, and restore it.

The decoder may share syntax machinery with Kitty/xterm-compatible CSI-u frames, but ownership semantics exist only for Kitty.

## 4. Traditional compatibility rules

1. Opening `TerminalSession` does not automatically enable modern keyboard reporting.
2. Existing traditional input remains the default and fallback.
3. Existing traditional byte decoding remains unchanged.
4. Traditional named/modified key events normalize to `TerminalKeyEventPhase.Press`.
5. Traditional plain text remains `TerminalInputEventKind.Text` and is not converted into synthetic key events.
6. The library never fabricates repeat/release, modern modifiers, alternate-layout keys, or associated text that were not reported by the terminal.

## 5. Frozen key-event phase API

0.17 adds:

```csharp
public enum TerminalKeyEventPhase {
	Press = 0,
	Repeat = 1,
	Release = 2
}
```

`TerminalInputEvent` adds:

```csharp
public TerminalKeyEventPhase? KeyPhase { get; }
```

Rules:

- `KeyPhase` is non-null only for `TerminalInputEventKind.Key`;
- traditional key events use `Press`;
- Kitty event type 1 maps to `Press`;
- Kitty event type 2 maps to `Repeat`;
- Kitty event type 3 maps to `Release`;
- no timing-based repeat inference exists.

`Press = 0` is intentional so the default enum value is the traditional semantic baseline.

## 6. Frozen modifier expansion

Existing values remain:

```csharp
Shift   = 1
Control = 2
Alt     = 4
```

0.17 appends:

```csharp
Super    = 8
Hyper    = 16
Meta     = 32
CapsLock = 64
NumLock  = 128
```

These are semantic `Icod.Terminal` bit values. Kitty's wire bit assignments for Alt and Control differ from the pre-existing public enum, so the decoder explicitly maps wire bits to semantic bits rather than casting the protocol integer directly.

No modern modifier is collapsed into another public identity.

## 7. Frozen named-key expansion

0.17 supports the current Kitty functional-key table semantically rather than leaking raw private-use integers.

Existing `TerminalKey.Function` continues to represent F1 through F35 using `FunctionKeyNumber`.

The following additional `TerminalKey` identities are appended:

```text
CapsLock
ScrollLock
NumLock
PrintScreen
Pause
Menu

Keypad0 .. Keypad9
KeypadDecimal
KeypadDivide
KeypadMultiply
KeypadSubtract
KeypadAdd
KeypadEnter
KeypadEqual
KeypadSeparator
KeypadLeft
KeypadRight
KeypadUp
KeypadDown
KeypadPageUp
KeypadPageDown
KeypadHome
KeypadEnd
KeypadInsert
KeypadDelete
KeypadBegin

MediaPlay
MediaPause
MediaPlayPause
MediaReverse
MediaStop
MediaFastForward
MediaRewind
MediaTrackNext
MediaTrackPrevious
MediaRecord
VolumeDown
VolumeUp
VolumeMute

LeftShift
LeftControl
LeftAlt
LeftSuper
LeftHyper
LeftMeta
RightShift
RightControl
RightAlt
RightSuper
RightHyper
RightMeta
IsoLevel3Shift
IsoLevel5Shift

Unrecognized
```

`Unrecognized` is a forward-compatibility semantic bucket for syntactically valid modern functional-key reports whose numeric identity is not known to the current library. The raw Kitty private-use integer is not exposed publicly.

Known current Kitty functional-key codes must never map to `Unrecognized`.

## 8. Character identity, alternate keys, and associated text

### 8.1 `Character`

Existing `TerminalInputEvent.Character` remains the character identity for `TerminalKey.Character`.

For Kitty CSI-u key events, the protocol's primary Unicode key code is the unshifted/current-layout key identity and is stored in `Character`.

Traditional text semantics remain unchanged.

### 8.2 Alternate key identities

0.17 adds to `TerminalInputEvent`:

```csharp
public Rune? ShiftedCharacter { get; }
public Rune? BaseLayoutCharacter { get; }
```

Rules:

- these values are reported only when supplied by the modern protocol;
- they are valid only for character-key events;
- shifted character is the protocol's shifted-layout key identity;
- base-layout character is the standard-layout physical-key identity described by Kitty;
- absent data remains `null`;
- no keyboard-layout inference is performed by the library.

### 8.3 Associated text

0.17 adds:

```csharp
public string? AssociatedText { get; }
```

Associated text is valid only for semantic key events decoded from a protocol that supplied it.

Rules:

- it preserves one or more Unicode scalar values in order;
- malformed UTF-16 is impossible because the decoder constructs it only from validated scalar values;
- Kitty-forbidden C0/C1 control code points in associated-text fields make the frame malformed;
- `Character` remains the key identity; `AssociatedText` is text produced by that key event;
- the library does not implement IME policy.

When Kitty reports key code `0` with associated text and therefore supplies no key identity, the text is emitted through the existing `TerminalInputEventKind.Text` path as ordinary Unicode scalar events. The library does not invent a key identity for pure text input.

## 9. Frozen keyboard reporting tiers

0.17 adds:

```csharp
public enum TerminalKeyboardReportingMode {
	Disambiguated = 0,
	EventTypes = 1,
	AllKeys = 2
}
```

`TerminalInputProtocolOptions` adds:

```csharp
public TerminalKeyboardReportingMode? KeyboardReportingMode { get; init; }
```

`TerminalInputProtocolLease` adds:

```csharp
public TerminalKeyboardReportingMode? KeyboardReportingMode { get; }
```

A null mode means no keyboard protocol request.

### Disambiguated

Semantic goal: remove legacy escape/control ambiguity while retaining ordinary UTF-8 text delivery.

Kitty flags:

```text
1 | 4 = 5
```

The library intentionally includes Kitty alternate-key reporting (`4`) in every negotiated Kitty tier because it only enriches escape-coded key reports and provides robust shortcut identity without forcing ordinary text keys into escape frames.

### EventTypes

Semantic goal: preserve repeat/release information where Kitty can report it while still retaining normal direct text delivery.

Kitty flags:

```text
1 | 2 | 4 = 7
```

Enter, Tab, Backspace, and text-producing keys that remain legacy/direct-text encoded do not gain release events in this tier. Such key events continue to normalize as `Press` when represented through legacy forms.

### AllKeys

Semantic goal: uniform key-event reporting, including text-producing keys and modifier keys, with event types and produced text retained.

Kitty flags:

```text
1 | 2 | 4 | 8 | 16 = 31
```

In this mode:

- all keyboard keys are escape-coded;
- press/repeat/release is available where the terminal reports it;
- modifier-key presses/releases are surfaced as named keys;
- alternate-key identities are accepted;
- associated text is accepted;
- text-producing key events are `TerminalInputEventKind.Key` with `TerminalKey.Character`, `Character`, `KeyPhase`, modifiers, and optional `AssociatedText`.

## 10. Strongest-request reconciliation

Keyboard reporting joins the existing `TerminalInputProtocolManager` lease domain.

Strength order:

```text
none < Disambiguated < EventTypes < AllKeys
```

Overlapping leases reconcile to the strongest active mode.

Because the Kitty flag sets are monotonic supersets (`5`, `7`, `31`), upgrading/downgrading the physical mode is deterministic.

A lease may request keyboard reporting together with paste, focus, and mouse reporting. No separate public keyboard lease is introduced.

## 11. Kitty support detection and acquisition

A keyboard-reporting request requires a Kitty-capable negotiated path.

The manager must not blindly emit a Kitty push and assume support. Acquisition uses the existing active-query infrastructure to determine protocol support according to the Kitty detection procedure: query keyboard flags and use a device-attributes response as the bounded completion delimiter.

If the terminal cannot establish Kitty support, `AcquireInputProtocolsAsync(...)` returns a controlled unavailable `TerminalControlResult<TerminalInputProtocolLease>` for a request that requires keyboard reporting.

The manager does not silently fall back to xterm `modifyOtherKeys` because xterm cannot satisfy the frozen `Disambiguated`, `EventTypes`, or `AllKeys` semantic guarantees truthfully.

## 12. Kitty stack ownership

The library uses Kitty push/pop rather than destructive flag replacement as its ownership primitive.

For the currently active screen:

- first keyboard owner pushes the desired flag set;
- stronger reconciliation replaces only library-owned physical state using a controlled pop/push transition;
- final owner release pops the library-owned stack entry;
- the library never pops an entry it did not push;
- stack depth owned by `Icod.Terminal` is bounded to one physical entry on the currently active screen.

The logical lease count can be larger; it does not map one-to-one to terminal stack depth.

## 13. Main/alternate-screen behavior

Kitty main and alternate screens maintain independent keyboard-mode stacks, so 0.17 must never leave an inactive-screen library-owned push behind after the semantic request disappears.

For screen transitions controlled through `TerminalPresentationManager`, the frozen ordering is:

### Enter alternate screen while keyboard reporting is active

1. pop the library-owned main-screen keyboard entry;
2. enter alternate screen;
3. push the desired keyboard flags on the alternate screen.

### Leave alternate screen while keyboard reporting is active

1. pop the library-owned alternate-screen keyboard entry;
2. leave alternate screen;
3. push the desired keyboard flags on the main screen.

Thus at most one library-owned Kitty stack entry exists, and it always belongs to the currently active managed screen.

If keyboard reporting is acquired while already in alternate screen, the push occurs only there. If the screen later returns to main through the managed presentation path while the request remains active, the mode is pushed on main after the transition.

External/unmanaged alternate-screen escape sequences cannot be observed reliably by `Icod.Terminal`; deterministic cross-screen keyboard ownership is guaranteed only for screen transitions made through the session's presentation manager.

## 14. xterm `modifyOtherKeys` compatibility policy

T174 will decode, at minimum:

- conventional `modifyOtherKeys` level-2 ordinary-key reports of the `CSI 27;modifier;key~` family;
- the xterm CSI-u formatting form where the frame is syntactically unambiguous.

These reports normalize to:

- `TerminalInputEventKind.Key`;
- `TerminalKey.Character` for ordinary character keys;
- supported Shift/Control/Alt semantic modifier flags;
- `TerminalKeyEventPhase.Press`;
- no alternate-layout, associated-text, lock-modifier, or release semantics unless those are actually present in a Kitty frame.

No `CSI >4;2m` activation is sent by the 0.17 lease manager.

## 15. Unknown and malformed modern frames

### Syntactically valid unknown functional key

A valid modern key frame with a functional numeric key code not known to the current library emits a `TerminalKey.Unrecognized` key event. Modifiers, phase, and associated text that are otherwise valid are preserved.

### Invalid numeric range or parameter count

The frame is malformed. Numeric accumulation and parameter/subparameter counts are bounded before allocation growth.

### Invalid Unicode scalar

The frame is malformed. Surrogates, values above U+10FFFF, and prohibited associated-text controls are rejected.

### Recovery

Malformed modern frames are consumed only through a bounded synchronization point: the decoder consumes the complete CSI frame when a final byte has been reached, or consumes a minimal offending prefix when the frame has already become impossible. It must not leave the decoder permanently waiting on an invalid frame.

The next valid input begins from a clean parser state.

Unknown/malformed keyboard frames must never complete an unrelated active terminal query.

## 16. Input/query routing priority

The existing response router retains priority for an active correlated terminal query.

Keyboard decoding must not steal frames that satisfy a currently active expectation such as CPR, DA, DECRQSS, XTGETTCAP, or the new Kitty keyboard-support query.

When no active expectation claims a syntactically valid keyboard frame, it may be decoded as application input.

This is particularly important because historical CSI-u forms overlap in shape with terminal control sequences in the opposite direction.

## 17. Lifecycle behavior

Keyboard reporting follows the existing input-protocol lifecycle contract:

- suspension pops/restores any current library-owned Kitty entry before process suspension;
- resume/reentry re-detects/re-establishes the desired current mode rather than assuming terminal state survived;
- `InvalidateState()` marks physical keyboard state unknown and requires controlled re-establishment before the manager claims it is active;
- disposal releases only library-owned keyboard state;
- no synthetic key events are emitted during lifecycle transitions;
- transition cancellation before output commitment emits nothing;
- committed transition writes are serialized and non-cancellable;
- transition failure propagates and cannot silently advance believed state.

## 18. Security and behavioral warning

Modern keyboard mode changes how control-key input reaches the application. For example, Kitty disambiguation can cause combinations such as Ctrl+C that traditionally become C0 bytes/signals to arrive as explicit escape-coded key events.

Applications acquiring modern keyboard reporting must not assume shell/line-discipline behavior remains identical to traditional mode.

0.17 documentation and samples must call this out explicitly.

## 19. Explicit exclusions

0.17 does not add:

- raw keyboard control-sequence APIs;
- public Kitty flag integers;
- public raw Kitty private-use key codes;
- global hotkeys;
- keyboard remapping/binding policy;
- OS keyboard hooks;
- physical scan-code contracts;
- automatic terminal-brand activation;
- IME configuration/control;
- synthetic repeat detection;
- fabricated release events;
- blind xterm `modifyOtherKeys` activation;
- automatic promotion of a configured plain CSI-u stream into a claimed lease-owned mode.

## 20. Testing obligations

T171–T175 must prove:

- all existing enum values remain unchanged;
- traditional decoder byte behavior remains unchanged;
- traditional key events normalize to `Press`;
- all appended modifier values are exact and round-trip;
- the complete frozen current Kitty functional-key table maps without leaking PUA integers;
- unknown functional codes map to `Unrecognized`;
- shifted/base-layout character metadata is preserved;
- associated text preserves multiple Unicode scalars;
- `Disambiguated`, `EventTypes`, and `AllKeys` semantics differ exactly as frozen;
- exact Kitty flag requests are `5`, `7`, and `31`;
- support detection is bounded and active-query-safe;
- push/pop ownership is nested and deterministic;
- managed main/alternate-screen transitions use pop/switch/push ordering;
- xterm compatibility decoding never fabricates Kitty-only semantics;
- fragmented/coalesced input is bounded;
- malformed and unknown frame recovery is deterministic;
- paste/focus/mouse composition remains intact;
- lifecycle suspend/resume/invalidation/disposal is safe;
- output failure/cancellation leaves believed state truthful;
- no raw/generic keyboard protocol API enters the package.

## 21. Frozen T170 conclusion

T170 is complete.

0.17 is now contractually anchored around:

1. traditional input as unchanged default/fallback;
2. an additive semantic key model with explicit `Press`/`Repeat`/`Release`;
3. distinct modern modifiers;
4. complete semantic coverage of the current Kitty functional-key table;
5. alternate-key and associated-text metadata without raw protocol leakage;
6. three explicit keyboard reporting modes mapping to Kitty flags `5`, `7`, and `31`;
7. Kitty as the only lease-owned negotiated keyboard protocol;
8. xterm `modifyOtherKeys` as decoder compatibility only;
9. one current-screen library-owned Kitty stack entry with managed screen-transition choreography;
10. bounded parser/query/lifecycle behavior.

Next: T171 — semantic key-event model.
