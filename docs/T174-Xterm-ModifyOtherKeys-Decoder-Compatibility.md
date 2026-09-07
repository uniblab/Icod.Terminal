# T174 — xterm modifyOtherKeys Decoder Compatibility

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T174  
**Version:** `0.17.0-alpha.5`  
**Status:** Implemented; exact-head validation pending

## Purpose

T174 adds decode-only compatibility for xterm `modifyOtherKeys` without making xterm mode part of the reversible protocol lease.

The library sends no `CSI > 4 ; ... m` activation or disable sequence. This preserves T170's ownership rule: `Icod.Terminal` does not mutate terminal keyboard state that it cannot query/push/pop and restore reliably.

## Supported xterm forms

### Conventional level-2 form

```text
CSI 27 ; modifier ; key ~
```

The frame is recognized only when the first parameter is exactly `27` and all three numeric parameters are valid.

`modifier` follows the conventional xterm `1 + bitset` encoding for:

```text
Shift   bit 0
Alt     bit 1
Control bit 2
```

No higher modifier bits are promoted into Kitty-only semantic identities.

`key` is decoded as a Unicode scalar. Existing semantic identities are used for Enter, Escape, Tab, Space and Backspace; all other valid Unicode scalars become `TerminalKey.Character`.

Every xterm level-2 event is:

```text
TerminalInputEventKind.Key
TerminalKeyEventPhase.Press
```

because xterm `modifyOtherKeys` does not carry Kitty event-type semantics.

### Unambiguous CSI-u form

Plain xterm/fixterms-style:

```text
CSI key ; modifier u
```

already shares the canonical CSI-u decoder introduced in T172. When no Kitty event-type/alternate-key/associated-text fields are present, it naturally normalizes to `Press` with the reported character identity and traditional modifiers.

T174 therefore does not duplicate CSI-u parsing.

## Explicitly absent semantics

xterm compatibility does not fabricate:

- Repeat or Release;
- Super, Hyper, Meta, CapsLock or NumLock modifier state;
- shifted-layout or base-layout key identities;
- associated text;
- Kitty private-use functional-key identity from xterm level-2 frames;
- negotiated keyboard ownership.

## Decoder placement

xterm level-2 frames use the same bounded incremental modern-keyboard path as Kitty CSI-u. Active correlated terminal-query routing retains priority.

Non-xterm `~` CSI sequences remain available to the existing terminfo/traditional decoder; only an exact valid `CSI 27;modifier;key~` shape is claimed by T174.

## Validation

Focused tests prove:

- Shift, Alt, Control and all-three combinations;
- Unicode scalar preservation;
- semantic Enter/Escape/Tab/Space/Backspace mapping;
- fragmented level-2 frames;
- coalesced xterm frame plus ordinary text;
- press-only behavior;
- absence of Kitty-only metadata;
- plain xterm-style CSI-u normalization through the shared decoder.

Next after green exact-head validation: T175 — cross-protocol composition, lifecycle/screen choreography, fault injection and hardening.
