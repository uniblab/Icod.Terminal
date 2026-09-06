# Icod.Terminal 0.17.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.17.0`  
**Development version:** `0.17.0-alpha.1`  
**Predecessor:** `0.16.0` — OSC 9 Safe Extensions  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** modern keyboard contracts and negotiated keyboard protocols  
**Status:** T170 contract/reference freeze in progress

---

## 1. Position on the road to 1.0

```text
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

The 0.17 release modernizes keyboard input without breaking the traditional terminal-input path. Existing applications that never request a modern keyboard protocol continue to receive the traditional decoded key/text model.

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

---

## 2. Design goals

0.17 SHALL:

- preserve the current traditional-key decoder and byte compatibility when no modern protocol is active;
- represent key press, repeat, and release semantics explicitly where the terminal protocol can report them;
- represent modern modifier state without collapsing distinct modifier identities into Alt/Control/Shift;
- decode negotiated Kitty keyboard protocol events into terminal-independent semantic events;
- support xterm `modifyOtherKeys` as a narrower compatibility protocol rather than pretending it is Kitty/CSI-u;
- keep protocol negotiation reversible and lease-owned through the existing `TerminalInputProtocolManager` architecture;
- compose modern keyboard requests with bracketed paste, focus reporting, and mouse tracking;
- preserve active terminal-query routing and incremental input framing;
- avoid terminal-brand heuristics when a protocol can instead be requested explicitly;
- expose semantic APIs rather than raw keyboard escape-sequence switches.

---

## 3. Compatibility constraints

The current public input model already defines:

- `TerminalInputEventKind.Text` and `TerminalInputEventKind.Key`;
- terminal-independent named keys via `TerminalKey`;
- `TerminalKeyModifiers.Shift`, `Control`, and `Alt`;
- traditional character/key decoding;
- reversible rich-input protocol ownership through `TerminalInputProtocolOptions` and `TerminalInputProtocolLease`.

0.17 evolves these contracts additively. Existing enum values and existing decoding semantics SHALL remain stable.

Traditional terminal input remains the fallback and default. Modern keyboard protocol acquisition is explicit.

---

## 4. Protocol tiers

### Tier A — traditional terminal keyboard input

Retained unchanged as the compatibility baseline. Traditional input produces press-like semantic events only because release/repeat distinction is not present on the wire.

### Tier B — xterm `modifyOtherKeys`

Supported as a compatibility protocol for disambiguating modified ordinary keys.

It is narrower than Kitty:

- primarily enriches modified-key encoding;
- does not provide the complete Kitty event-type/modifier model;
- must not be represented publicly as “Kitty mode” or “full CSI-u”.

0.17 will decode the forms actually negotiated by the library and normalize them into the shared semantic key model.

### Tier C — Kitty keyboard protocol

The preferred modern keyboard protocol for applications that explicitly request rich keyboard semantics.

0.17 will use the protocol's application-negotiated mode rather than terminal-brand detection. The implementation will initially request only the smallest flag set required by the public 0.17 contract and will restore/pop its owned mode deterministically.

---

## 5. Semantic key model direction

T170 freezes the exact public shape before production implementation, but the intended additive model is:

- retain `TerminalInputEventKind.Key`;
- add a key-event phase/type describing `Press`, `Repeat`, or `Release`;
- retain all existing `TerminalKey` values and append any additional named-key identities required by the modern protocols;
- retain existing `Shift`, `Control`, and `Alt` flag values;
- add distinct modern modifier flags such as `Super`, `Hyper`, `Meta`, `CapsLock`, and `NumLock` where the source protocol can report them;
- preserve a Unicode `Rune` for character-bearing key events;
- preserve function-key numbering for `TerminalKey.Function`;
- do not expose raw Kitty numeric key codes as the primary public contract.

Traditional events normalize to `Press` and retain their current modifiers.

---

## 6. Negotiation and ownership direction

Modern keyboard mode will integrate with the existing input-protocol lease model.

A keyboard request SHALL be:

- explicitly requested by application code;
- reversible;
- nestable/overlappable with other leases;
- reconciled by the manager to the strongest active keyboard request;
- restored when the last requesting lease is released;
- suspended/restored according to the existing lifecycle-safe protocol-manager rules;
- independent of application output such as OSC 9, colors, synchronized output, or OSC 133.

The manager must avoid protocol-state corruption when multiple callers request compatible or stronger keyboard capabilities concurrently.

---

## 7. Explicit exclusions from 0.17

Unless T170 reference work demonstrates a compelling interoperability requirement, 0.17 will not add:

- terminal-brand auto-detection as a prerequisite for keyboard mode;
- arbitrary raw `CSI > ...`/`CSI < ...` keyboard-control APIs;
- a generic “send keyboard protocol escape sequence” API;
- application-visible Kitty numeric private-use key codes where a stable semantic key identity can be used instead;
- physical scan-code APIs;
- OS-native keyboard-hook APIs;
- global hotkeys;
- IME control or composition policy;
- Windows Console `KEY_EVENT_RECORD` as a new public terminal protocol abstraction;
- keyboard remapping/binding policy (belongs to higher-level consumers such as `Icod.DCurses`).

---

## 8. Tranche plan

### T170 — modern keyboard contract/reference freeze — `0.17.0-alpha.1`

Freeze:

- protocol reference tiers and supported wire forms;
- exact additive public event/modifier/key contracts;
- Kitty flag/event-type scope;
- xterm `modifyOtherKeys` scope;
- negotiation/lease semantics;
- fallback/traditional compatibility;
- malformed/unknown input behavior;
- lifecycle, cancellation, and response-routing rules;
- test obligations and explicit exclusions.

Record: `docs/T170-Modern-Keyboard-Contract-and-Reference-Freeze.md`.

### T171 — semantic key-event model — `0.17.0-alpha.2`

Implement the frozen additive public event model before adding any modern wire decoder.

Expected work:

- key event type/phase;
- expanded modifier flags;
- required named-key additions;
- constructor/factory invariants;
- traditional decoder normalization to press events;
- API/XML/unit tests proving existing values and behavior remain compatible.

### T172 — Kitty keyboard decoder foundation — `0.17.0-alpha.3`

Implement incremental Kitty/CSI-u event parsing for the frozen supported forms.

Cover:

- character keys;
- named functional keys in scope;
- modifiers;
- press/repeat/release event type;
- UTF-8/Unicode scalar correctness;
- partial frames and bounded parameter parsing;
- malformed and unsupported sequences without poisoning subsequent input;
- interaction with terminal-response framing/routing.

### T173 — negotiated Kitty keyboard ownership — `0.17.0-alpha.4`

Integrate the preferred modern keyboard request into `TerminalInputProtocolOptions`, `TerminalInputProtocolLease`, and `TerminalInputProtocolManager`.

Prove:

- explicit acquisition/release;
- nested ownership;
- strongest-request reconciliation;
- push/pop or equivalent protocol-safe restoration;
- cancellation before output commitment;
- one complete control-frame write per transition;
- suspend/resume lifecycle behavior;
- no terminal-brand heuristics.

### T174 — xterm `modifyOtherKeys` compatibility — `0.17.0-alpha.5`

Add the frozen narrower xterm compatibility tier.

Prove:

- negotiated enable/disable behavior;
- exact supported wire forms;
- modified ordinary-key disambiguation;
- normalization into the same semantic event model;
- no false promotion to Kitty-only semantics;
- coexistence/reconciliation rules with Kitty requests.

### T175 — composition and hardening — `0.17.0-alpha.6`

Exercise modern keyboard input with:

- traditional keys/text;
- paste/focus/mouse protocols;
- active terminal queries;
- fragmented/coalesced reads;
- malformed input and recovery;
- concurrent protocol leases;
- suspend/resume/invalidation/disposal;
- output failures during protocol transitions;
- bounded input and deadlock/lock-order tests.

### T176 — downstream `Icod.DCurses` acceptance — `0.17.0-alpha.7`

Extend real `Icod.DCurses` acceptance using only public APIs.

Validate that a full-screen consumer can:

- acquire modern keyboard mode;
- receive semantic press/repeat/release events and richer modifiers;
- continue receiving ordinary text, focus, paste, and mouse events;
- refresh output while input mode is active;
- release/restorе the protocol deterministically.

Acceptance must run on net8/net9/net10 and validate deterministic protocol bytes/events rather than depending on the CI runner terminal emulator.

### T177 — public API/package/stable closure — `0.17.0`

Deliver:

- `docs/Public-API-Baseline-0.17.md`;
- README and focused sample updates;
- keyboard security/interoperability documentation;
- XML documentation assertions;
- fresh NuGet-only net8/net9/net10 consumer;
- retained 0.8–0.16 package gates;
- new 0.17 package contract in PR/main/tag validation;
- stable package metadata/release notes;
- exact-head validation before merge and exact-main Release validation before tag.

---

## 9. Required testing matrix

0.17 SHALL prove at minimum:

- all existing traditional-key tests remain green;
- existing enum values used by 0.16 and earlier remain unchanged;
- traditional keys normalize to press events;
- modern modifiers are preserved distinctly;
- Kitty character/named-key press, repeat, and release decoding;
- xterm `modifyOtherKeys` decoding for the frozen supported forms;
- incomplete/malformed sequence recovery;
- unknown modern key codes follow a documented safe fallback/error policy;
- protocol acquisition/release is exact and reversible;
- overlapping leases reconcile deterministically;
- lifecycle suspend/resume does not leak or double-pop keyboard modes;
- active-query routing remains correct while modern keyboard input is flowing;
- no raw/generic keyboard-control API enters the public package;
- Windows/Linux/macOS CI and net8/net9/net10 package-only consumers.

---

## 10. Current development state

```text
VersionPrefix:    0.17.0
VersionSuffix:    alpha.1
Version:          0.17.0-alpha.1
PackageVersion:   0.17.0-alpha.1
AssemblyVersion:  0.17.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Current work:** T170 — modern keyboard contract/reference freeze.
