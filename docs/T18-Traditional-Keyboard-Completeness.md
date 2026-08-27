# T18 — Traditional Keyboard Completeness

**Project:** `Icod.Terminal`
**Development line:** `0.2.0`
**Development version:** `0.2.0-alpha.6`
**Tranche:** T18 — traditional keyboard completeness
**Reference branch:** `0.2.0`
**Status:** Complete — local, CI, package, and prerelease validation passed

---

## 1. Purpose

T18 completes the traditional terminal-key path needed by `0.2.0` before the
DCurses acceptance tranche.

The work extends the existing capability-driven `TerminalInputDecoder`. It does
not introduce modern keyboard negotiation, a second keyboard parser, or a
catch-all CSI interpreter.

The principal additions are:

- shifted navigation and editing keys represented by standard terminfo
  capabilities;
- traditional Shift/Alt/Control combinations represented by ncurses/xterm
  extended key capabilities;
- semantic normalization of conventional modified function-key banks;
- explicit compatibility coverage for xterm-family, Windows Terminal, and
  Linux-console-style capability data.

---

## 2. Existing public event contract

T18 requires no new public key enum or modifier flag.

The T14 contract already provides:

```text
TerminalKey
TerminalKeyModifiers.Shift
TerminalKeyModifiers.Control
TerminalKeyModifiers.Alt
TerminalInputEvent.FunctionKeyNumber
```

Traditional combinations are therefore represented by combining the existing
modifier flags. A key carrying Shift+Alt+Control uses all three flags.

The public `TerminalKey.Function` event continues to carry the physical/base
function-key number through `FunctionKeyNumber`.

---

## 3. Standard shifted terminfo capabilities

T18 consumes the standard shifted capabilities already represented by
`Icod.TermInfo 1.0.0`:

```text
kDC    Shift+Delete
kEND   Shift+End
kHOM   Shift+Home
kIC    Shift+Insert
kLFT   Shift+Left
kNXT   Shift+PageDown
kPRV   Shift+PageUp
kRIT   Shift+Right
```

Each capability is decoded only when it is present in the selected
`TerminalDescription`.

Shift+Up and Shift+Down are not inferred from the standard `kind`/`kri`
scroll-key capabilities because those names do not semantically guarantee an
arrow key. The xterm/ncurses extended `kUP` and `kDN` capabilities are used when
present.

---

## 4. Extended traditional modifier capabilities

The xterm/ncurses extended key namespace represents modifier combinations with
a numeric suffix.

T18 recognizes the following families when the selected terminal advertises
them:

```text
kUPn   Up
kDNn   Down
kLFTn  Left
kRITn  Right
kHOMn  Home
kENDn  End
kPRVn  PageUp
kNXTn  PageDown
kICn   Insert
kDCn   Delete
```

The traditional modifier parameters are normalized as:

```text
2   Shift
3   Alt
4   Shift + Alt
5   Control
6   Shift + Control
7   Alt + Control
8   Shift + Alt + Control
```

The unsuffixed extended `kUP` and `kDN` names are treated as Shift+Up and
Shift+Down, matching the xterm-family capability contract supplied by
`Icod.TermInfo`.

No sequence is synthesized merely because its CSI shape looks familiar. The
extended capability must be present in the selected terminal description.

---

## 5. Modified function-key normalization

Traditional xterm-family terminfo entries frequently encode modified F1-F12
keys in the higher `kf13` through `kf63` capability slots.

For example, xterm uses:

```text
kf13   CSI 1;2P     Shift+F1
kf17   CSI 15;2~    Shift+F5
```

Windows Terminal also supplies conventional banks using modifier parameters 2,
3, 4, 5, and 6.

T18 does not assume that every `kf13` is Shift+F1 or that every higher-numbered
function capability is a modifier alias. A capability is normalized only when
its exact advertised value matches one of the traditional modified F1-F12 wire
forms:

```text
CSI 1 ; modifier P/Q/R/S
CSI 15/17/18/19/20/21/23/24 ; modifier ~
```

When such a match exists, the event reports the base function key and the
semantic modifier flags. For example:

```text
kf13 = CSI 1;2P

TerminalKey.Function
FunctionKeyNumber = 1
Modifiers = Shift
```

A `kf13` value which does not match a traditional modified-function form remains
`FunctionKeyNumber = 13` with no inferred modifier. This preserves genuine
physical F13 and terminal-specific function-key definitions.

The existing F0-F63 coverage remains available.

---

## 6. Incremental and ambiguity behavior

T18 adds exact sequences to the same `keySequences` table used by the existing
keyboard decoder.

The existing guarantees therefore continue to apply:

- a sequence may be fragmented at any byte boundary;
- longer overlapping advertised sequences win when continuation arrives;
- Escape-prefixed ambiguity is bounded by `EscapeSequenceTimeout`;
- `MaximumBufferedBytes` remains authoritative;
- mouse, focus, and paste continue to share the same input buffer and event
  ordering.

T18 does not add a general parser which accepts arbitrary CSI parameter lists.
An unknown CSI sequence therefore continues through the existing deterministic
fallback path rather than being swallowed or guessed into a key event.

---

## 7. Compatibility fixtures

Validation covers representative capability contracts rather than hard-coding
behavior from the host operating system.

### 7.1 xterm family

The built-in `Icod.TermInfo.TerminalProfiles.Xterm` fixture verifies:

- shifted navigation and editing;
- Alt, Shift+Alt, Control, Shift+Control, and Alt+Control navigation;
- conventional shifted F1-F12 normalization.

### 7.2 Windows Terminal

The built-in `TerminalProfiles.MsTerminal` fixture verifies the conventional
higher function-key banks used for Control, Shift+Control, Alt, and Shift+Alt
F1-F12 reports.

### 7.3 Linux-console-style data

A capability fixture using the traditional Linux-console F1 form `ESC[[A`
verifies that non-xterm wire shapes remain driven by terminfo and continue to
use the ordinary exact-sequence decoder.

The tests do not require the CI runner to expose a real interactive terminal.

---

## 8. Explicitly deferred

T18 does not add:

- CSI-u keyboard reporting;
- Kitty keyboard protocol negotiation;
- modifyOtherKeys negotiation;
- terminal query/response routing;
- heuristic Alt+printable-character folding from an Escape prefix;
- a general-purpose CSI keyboard grammar;
- consumer-specific DCurses input policy.

Those areas either remain assigned to later milestones or require negotiation
and ambiguity policy beyond the traditional capability-driven contract.

---

## 9. Completion gate

T18 is complete when:

1. `net8.0`, `net9.0`, and `net10.0` build and test;
2. standard shifted navigation/editing capabilities produce the expected
   semantic key plus `Shift`;
3. extended modifier capabilities produce the expected combined modifier flags;
4. traditional modified function-key forms normalize to base F1-F12 plus
   modifiers;
5. nonconventional higher function-key capabilities retain their advertised
   F-number;
6. F0-F63 coverage remains intact;
7. fragmented modified-key input remains incremental and bounded;
8. unknown CSI input is not consumed as an invented keyboard event;
9. keyboard, mouse, focus, paste, timeout, cancellation, and lifecycle behavior
   remain on the single `TerminalSession` event path;
10. package verification and fresh package consumers remain green on Windows,
    Linux, and macOS.

The T18 validation gate passed locally and in GitHub Actions. The
`0.2.0-alpha.6` prerelease was published successfully and became the package
consumed by the DCurses T19 acceptance work.

The next tranche is T19 — `Icod.DCurses` Integration and Rich-Input Acceptance.
