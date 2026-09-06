# T170 — Modern Keyboard Contract and Reference Freeze

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T170  
**Version:** `0.17.0-alpha.1`  
**Status:** Reference tiers frozen; exact public contract refinement in progress

## 1. Purpose

Traditional terminal keyboard encoding is inherently ambiguous. Distinct key combinations can produce the same bytes, modern modifiers are poorly represented, and traditional input has no reliable key-release/repeat distinction.

0.17 addresses this by adding an explicit modern keyboard semantic layer and negotiated protocol support while preserving the existing traditional decoder as the default compatibility baseline.

The release does **not** expose arbitrary keyboard escape sequences. Applications request semantic keyboard capabilities; `Icod.Terminal` owns protocol negotiation, decoding, nesting, lifecycle restoration, and fallback behavior.

## 2. Current `Icod.Terminal` baseline

Before 0.17 the public model provides:

- `TerminalInputEventKind.Text` for ordinary Unicode text;
- `TerminalInputEventKind.Key` for named or modified key events;
- `TerminalKey` identities for Character, Enter, Space, Escape, Backspace, Tab, arrows, Home/End, PageUp/PageDown, Insert/Delete, and numbered function keys;
- `TerminalKeyModifiers.Shift`, `Control`, and `Alt`;
- no public press/repeat/release distinction;
- no Super/Hyper/Meta/lock modifier flags;
- reversible rich-input ownership for paste/focus/mouse through `TerminalInputProtocolOptions` / `TerminalInputProtocolLease`.

These existing values and traditional decoding semantics are compatibility constraints for 0.17.

## 3. Reference tier: Kitty comprehensive keyboard protocol

The Kitty keyboard protocol is the preferred 0.17 modern tier because it is application-negotiated, progressively enhanced, and explicitly addresses ambiguities in traditional terminal input.

Relevant protocol facts frozen for 0.17 design:

- applications can push keyboard flags with `CSI > flags u`;
- applications can pop keyboard-mode stack entries with `CSI < number u`, defaulting to one entry;
- current flags can be queried with `CSI ? u` and reported as `CSI ? flags u`;
- flag `1` disambiguates escape codes;
- flag `2` reports press/repeat/release event types;
- flag `4` reports alternate keys;
- flag `8` reports all keys as escape codes;
- flag `16` reports associated text;
- modifier reporting distinguishes Shift, Alt, Control, Super, Hyper, Meta, CapsLock, and NumLock;
- event types distinguish press, repeat, and release;
- the protocol assigns stable private-use numeric identities to non-Unicode functional keys;
- main and alternate screens maintain independent keyboard-mode stacks.

0.17 uses these mechanisms as negotiated protocol state, never as a terminal-brand heuristic.

## 4. Reference tier: xterm `modifyOtherKeys`

xterm `modifyOtherKeys` remains relevant as a compatibility tier because deployed terminals and applications use it to disambiguate modified ordinary keys. xterm also supports a CSI-u formatting form for these reports.

However, it is **not** equivalent to the Kitty protocol:

- it does not provide the same release-event contract;
- it has narrower modifier semantics;
- it does not provide Kitty's progressive-enhancement stack model;
- different configuration levels/forms exist;
- it cannot truthfully satisfy the richest 0.17 keyboard reporting tier.

Therefore 0.17 treats `modifyOtherKeys` as a secondary compatibility protocol and normalizes only semantics it can actually report.

## 5. Plain fixterms/CSI-u stance

A globally configured fixterms/CSI-u mode is not treated as an independently negotiated protocol tier in 0.17.

Reasons:

- applications cannot robustly request/detect the plain mode in the same way as Kitty progressive enhancement;
- CSI-u has historical terminal-to-host/host-to-terminal ambiguity in broader terminal standards;
- terminal implementations differ in how plain CSI-u is enabled.

The decoder may share parsing machinery for syntactically compatible input, but public ownership semantics are defined only for protocols that `Icod.Terminal` can explicitly negotiate and restore.

## 6. Frozen compatibility rules

1. **Traditional mode is the default.** Opening `TerminalSession` does not automatically enable Kitty or xterm keyboard extensions.
2. **No terminal-brand detection.** The library does not infer modern keyboard mode from `$TERM`, terminal names, process environment variables, or executable identity.
3. **Existing traditional decoding remains valid.** Applications that do not acquire modern keyboard reporting see the same traditional key/text behavior as 0.16.
4. **Modern input is additive.** Existing public enum values are not renumbered or repurposed.
5. **Protocol truthfulness beats artificial uniformity.** A narrower protocol never fabricates release events, lock modifiers, or richer identities it did not receive.
6. **Unknown/malformed modern frames must not poison later input.** Decoder recovery is mandatory and bounded.

## 7. Semantic event-type direction

0.17 will add a public key-event type/phase with the semantic identities:

```text
Press
Repeat
Release
```

Traditional key events normalize to `Press` because their wire encoding carries no repeat/release distinction.

For Kitty input, the decoded event type is preserved exactly when the requested reporting tier includes event types.

A protocol that cannot distinguish repeat/release reports `Press`; the library does not infer repeat from timing.

## 8. Modifier direction

Existing flags retain their numeric values:

```text
Shift   = 1
Control = 2
Alt     = 4
```

0.17 will add distinct flags for modern modifier identities that Kitty can report:

```text
Super
Hyper
Meta
CapsLock
NumLock
```

No existing flag is repurposed. In particular:

- `Meta` is not silently collapsed into `Alt`;
- `Super` is not silently collapsed into `Control`;
- lock modifiers remain distinguishable from momentary modifiers.

The exact appended bit values will be frozen before T171 implementation.

## 9. Named-key direction

Existing `TerminalKey` identities remain unchanged.

T170 will freeze an additive set of named keys required to represent the 0.17 supported Kitty functional-key subset truthfully. Candidates include at least:

- CapsLock;
- ScrollLock;
- NumLock;
- PrintScreen;
- Pause;
- Menu.

Function keys continue to use `TerminalKey.Function` plus `FunctionKeyNumber` where possible rather than adding F1/F2/etc enum members.

Keypad/media/modifier-only/private functional keys will be included only if the 0.17 reporting tiers need them for a coherent semantic contract. Unsupported Kitty private-use numeric codes will not leak directly into the primary public API.

## 10. Reporting-tier problem and initial decision

Kitty progressive enhancement creates a real semantic tradeoff:

- flags `1|2` provide escape disambiguation plus event types for modern escape-encoded keys, but Enter/Tab/Backspace remain legacy bytes and therefore do not receive release events;
- flag `8` (`Report all keys as escape codes`) is required for fully uniform key-event reporting, including those keys;
- once all keys are escape-encoded, flag `16` (`Report associated text`) is important for preserving text generated by the keyboard/OS rather than reducing input to only a key identity.

0.17 will therefore expose **more than one semantic reporting intensity** instead of claiming that one negotiated mode satisfies every consumer.

The intended tiers are:

### Disambiguated

Request Kitty flag `1`.

Goal: resolve legacy escape/control ambiguities while preserving ordinary text delivery and maximizing compatibility.

### EventTypes

Request Kitty flags `1|2`.

Goal: add repeat/release semantics where the protocol can report them without forcing all text-producing keys into escape frames.

### AllKeys

Request a frozen combination based on `1|2|8|16`.

Goal: provide uniform key press/repeat/release events, including text-producing/control keys, while retaining associated text.

Whether alternate-key flag `4` belongs in `AllKeys` or a separate higher tier remains an explicit T170 decision; it will not be enabled accidentally.

## 11. Associated text

Kitty can attach text code points to an encoded key event. This may contain multiple Unicode scalar values.

The existing `TerminalInputEvent.Character` property can represent only one `Rune`, so a truthful `AllKeys` contract likely requires an additive associated-text property rather than overloading `Character`.

Frozen principles:

- associated text is semantic text produced by the key event, not a raw protocol string;
- it must be validated as Unicode scalar data;
- multiple code points must be representable without data loss;
- existing `Character` semantics remain intact for traditional/single-character key events;
- no IME policy is implemented by `Icod.Terminal`; the library reports what the terminal protocol supplies.

The exact public property type/name is still under T170 refinement.

## 12. Protocol ownership

Modern keyboard reporting joins the existing `TerminalInputProtocolManager` ownership domain.

Frozen ownership rules:

- acquisition is explicit;
- one `TerminalInputProtocolLease` may request keyboard reporting together with paste/focus/mouse reporting;
- overlapping leases reconcile to the strongest active compatible keyboard request;
- releasing a stronger lease restores the strongest remaining request;
- releasing the final keyboard owner restores/pops the library-owned keyboard protocol state;
- no unrelated lease may disable another owner's request;
- cancellation before transition output commitment emits nothing;
- committed control transitions use complete serialized writes;
- transition failure propagates and must not silently corrupt the manager's believed state.

Kitty's native push/pop stack is preferred for Kitty ownership because it preserves the terminal's pre-existing application state without requiring the library to guess that state.

## 13. Main/alternate-screen semantics

Kitty specifies independent keyboard-mode stacks for main and alternate screens.

`Icod.Terminal` does not currently own a generic alternate-screen policy; higher-level consumers such as `Icod.DCurses` may enter/leave the alternate screen.

T170 therefore requires the implementation to avoid assuming that a keyboard-mode push performed on one screen can be safely popped on another. Exact integration with screen transitions/lifecycle must be frozen before T173.

This is a hard correctness requirement, not a documentation footnote.

## 14. Input routing and parser boundaries

Modern keyboard parsing shares the existing incremental input path.

Frozen rules:

- terminal query responses retain correlation priority and must not be misreported as key events;
- fragmented modern key frames remain buffered until complete or invalid;
- coalesced key events are emitted one semantic event at a time;
- parameter counts and numeric values are bounded;
- unknown Kitty functional-key codes follow a documented safe policy rather than throwing from the background reader;
- malformed modern frames do not permanently stall input;
- plain UTF-8 text remains text in tiers where Kitty sends text directly.

## 15. Security and robustness

Modern keyboard mode changes how control-key input reaches the application. For example, Kitty disambiguation can cause combinations that traditionally become C0 bytes/signals to arrive as escape-coded key events.

0.17 documentation must make this explicit. Applications must not assume that acquiring modern keyboard mode preserves shell/line-discipline behavior for every control combination.

Additional requirements:

- no unbounded numeric/parameter allocation;
- no unbounded keyboard-mode stack manipulation by the library;
- no raw protocol escape API;
- no environment/terminal-brand probing to decide whether to mutate keyboard mode;
- deterministic release on normal disposal and lifecycle restoration paths;
- failure tests for interrupted acquisition/release.

## 16. Explicit exclusions

0.17 does not add:

- raw keyboard control-sequence APIs;
- global hotkeys;
- keyboard remapping/binding policy;
- OS keyboard hooks;
- physical scan-code contracts;
- automatic terminal-brand activation;
- IME configuration/control;
- synthetic repeat detection from elapsed time;
- fabricated release events for protocols that cannot report them;
- generic exposure of Kitty private-use numeric codes as the stable semantic API.

## 17. T170 decisions still to freeze

Before T170 closes, the following must be made exact:

1. public name/type for key event phase;
2. exact appended modifier bit values;
3. exact additional `TerminalKey` identities;
4. public keyboard reporting-tier enum/property names;
5. associated-text property shape;
6. whether alternate-key flag `4` is in scope for 0.17;
7. exact xterm `modifyOtherKeys` acquisition level/format and reconciliation with Kitty requests;
8. unknown Kitty functional-key policy;
9. main/alternate-screen ownership behavior;
10. precise malformed-frame fallback/recovery semantics.

No production API implementation should precede these decisions.

## 18. Testing obligations

T171–T175 must prove:

- existing enum values remain unchanged;
- traditional decoder byte behavior remains unchanged;
- traditional key events normalize to Press;
- all new modifier flags round-trip through semantic events;
- Kitty press/repeat/release decoding for the frozen tiers;
- associated text without Unicode loss when requested;
- bounded fragmented/coalesced parsing;
- malformed/unknown frame recovery;
- exact Kitty push/pop bytes and nesting;
- exact xterm compatibility bytes for the frozen supported mode;
- paste/focus/mouse composition;
- active-query correlation under modern key traffic;
- lifecycle/suspend/resume/disposal restoration;
- output-failure/cancellation consistency;
- no raw/generic keyboard protocol API in the shipped package.

## 19. Initial T170 conclusion

The 0.17 architecture is now anchored around:

1. traditional input as stable fallback/default;
2. Kitty progressive enhancement as the preferred modern protocol;
3. xterm `modifyOtherKeys` as a narrower compatibility tier;
4. additive semantic event/modifier/key contracts;
5. explicit reporting intensities rather than one misleading “modern keyboard enabled” boolean;
6. integration into the existing reversible input-protocol lease manager;
7. strict parser/routing/lifecycle safety.

The remaining T170 decisions concern exact public type names/values and the full-key/associated-text boundary. Those will be frozen before T171 begins.
