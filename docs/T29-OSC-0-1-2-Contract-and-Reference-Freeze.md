# T29 — OSC 0 / 1 / 2 Contract and Reference Freeze

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Tranche:** T29  
**Theme:** OSC 0 / OSC 1 / OSC 2 title operations and safe outbound OSC framing  
**Status:** Contract frozen for T30 implementation

---

## 1. Purpose

T29 freezes the protocol, encoding, safety, capability, API-shape, and resource decisions required before implementation of OSC title emission begins.

The tranche is deliberately contract-only. T29 does **not** add an OSC writer or public title APIs. T30 and later tranches implement this frozen contract.

The principal design objective is to use OSC 0/1/2 as the smallest useful operational-protocol family with which to establish a reusable, injection-safe outbound OSC foundation for later OSC 7, OSC 8, and OSC 52 work.

---

## 2. Authoritative references

### 2.1 ECMA-48 control-string framing

ECMA-48 5th edition, section 5.6, defines a control string as an opening delimiter, a command or character string, and the terminating delimiter STRING TERMINATOR (ST). OSC is one of the defined opening delimiters.

T29 therefore treats ECMA-48 as authoritative for the generic OSC/ST control-string structure, but **not** for the semantics of OSC selectors 0, 1, or 2; those selector meanings are outside ECMA-48 and come from terminal-family documentation.

Reference:

- ECMA-48, 5th edition, section 5.6, *Control strings*.

### 2.2 xterm OSC 0 / 1 / 2 semantics

The xterm control-sequence specification defines:

```text
OSC Ps ; Pt BEL
OSC Ps ; Pt ST
```

and assigns:

```text
Ps = 0  Change Icon Name and Window Title to Pt
Ps = 1  Change Icon Name to Pt
Ps = 2  Change Window Title to Pt
```

xterm accepts BEL or ST as an OSC terminator and documents ST as the ECMA-48 string terminator; BEL remains supported for legacy applications.

Reference:

- XTerm Control Sequences, *Operating System Commands*, current pinned project reference at T29 implementation time: xterm Patch #411 documentation, 2026-08-23.

### 2.3 Compatibility target

`Icod.Terminal 0.4.0` targets the widely implemented xterm-family OSC 0/1/2 contract. It does not claim that ECMA-48 itself standardizes title-setting semantics.

Terminal-specific deviations discovered later SHALL be represented as compatibility policy or documented limitations. They SHALL NOT silently rewrite the selector contract frozen here.

---

## 3. Frozen wire forms

### 3.1 7-bit controls are canonical

`Icod.Terminal` SHALL emit the 7-bit OSC introducer and 7-bit ST terminator:

```text
OSC introducer: ESC ]     bytes 1B 5D
ST terminator:  ESC \     bytes 1B 5C
```

The single-byte C1 forms `0x9D` (OSC) and `0x9C` (ST) SHALL NOT be emitted by the 0.4.0 writer.

Rationale:

- 7-bit forms are portable through UTF-8-oriented byte streams;
- they avoid dependence on an 8-bit C1 transport interpretation;
- they are directly compatible with the xterm family and common modern terminal implementations.

### 3.2 Canonical terminator

`Icod.Terminal 0.4.0` SHALL terminate outbound OSC 0/1/2 frames with **ST (`ESC \`)**, not BEL.

Rationale:

- ST is the ECMA-48 control-string terminator;
- xterm accepts ST for OSC and identifies BEL as a legacy-compatible alternative;
- ST avoids creating an audible/visual bell if a malformed or partially interpreted frame reaches an unusual endpoint.

BEL termination MAY be supported in a later compatibility layer if concrete evidence requires it. No public terminator option is introduced by 0.4.0.

### 3.3 Exact supported frames

For a validated UTF-8 payload `Pt`:

```text
OSC 0: ESC ] 0 ; Pt ESC \
OSC 1: ESC ] 1 ; Pt ESC \
OSC 2: ESC ] 2 ; Pt ESC \
```

Byte prefixes and suffixes are therefore:

```text
OSC 0 prefix: 1B 5D 30 3B
OSC 1 prefix: 1B 5D 31 3B
OSC 2 prefix: 1B 5D 32 3B
ST suffix:    1B 5C
```

No omitted-selector shorthand is supported.

---

## 4. Payload encoding contract

### 4.1 Managed input model

The semantic title operations SHALL accept managed text and encode it as UTF-8 bytes for the terminal output stream.

The encoder SHALL use strict UTF-8 conversion. Ill-formed UTF-16 input, including unpaired surrogates, SHALL be rejected rather than replacement-encoded silently.

### 4.2 Empty payload

An empty title payload is valid and SHALL be emitted as an empty `Pt` field. Whether a particular terminal interprets that as clearing a title is terminal behavior; `Icod.Terminal` promises only the emitted protocol operation.

### 4.3 Forbidden payload characters

To prevent OSC termination, control injection, and ambiguous host behavior, 0.4.0 SHALL reject title strings containing Unicode control characters in these ranges:

```text
U+0000..U+001F   C0 controls, including NUL, BEL and ESC
U+007F           DEL
U+0080..U+009F   C1 controls
```

This deliberately rejects newline, carriage return, tab, BEL, ESC, embedded NUL, OSC/ST controls, and other control characters rather than attempting to sanitize them.

The 0.4.0 policy is **reject, do not strip and do not escape**.

Rationale:

- title operations are metadata, not a general text channel;
- silent stripping would make the output differ from the caller's input;
- generic escaping would create terminal-specific display semantics;
- rejecting the complete control ranges gives the later OSC foundation a simple and auditable injection invariant.

### 4.4 Printable Unicode

All other valid Unicode scalar values are eligible for strict UTF-8 encoding, subject to the byte-size limit below.

`Icod.Terminal` does not normalize Unicode title text. Normalization, bidi policy, localization, and presentation are caller concerns unless a future security review identifies a terminal-protocol requirement.

---

## 5. Resource limit

The maximum encoded `Pt` payload for OSC 0/1/2 in 0.4.0 SHALL be **4096 UTF-8 bytes**.

This is an `Icod.Terminal` safety/resource limit, not a claim that xterm or ECMA-48 define a 4096-byte maximum.

The limit is measured **after strict UTF-8 encoding** and excludes the OSC introducer, selector, semicolon, and ST terminator.

A payload whose UTF-8 representation exceeds 4096 bytes SHALL be rejected before any bytes are written.

Rationale:

- title strings are expected to be human-scale metadata;
- a bounded payload prevents accidental multi-megabyte control frames;
- measuring encoded bytes makes memory/wire cost deterministic across ASCII and non-ASCII text;
- the limit can be reconsidered in a later pre-1.0 release if real terminal compatibility evidence requires it.

No public per-call override is introduced in 0.4.0.

---

## 6. Atomic validation-before-write rule

All validation and UTF-8 length checking SHALL complete **before the first byte of an OSC frame is written**.

If validation fails:

- no OSC introducer is emitted;
- no partial payload is emitted;
- no ST terminator is emitted;
- the output stream remains untouched by that operation.

T30/T34 SHALL preserve complete-frame serialization so another terminal-control operation or ordinary application write cannot interleave inside a title frame.

---

## 7. Capability and endpoint semantics

### 7.1 Capability data versus live support

`Icod.TermInfo` remains the static capability authority, but OSC 0/1/2 support is not uniformly or reliably represented by terminfo data.

Therefore:

- absence of a dedicated terminfo capability SHALL NOT be treated as proof that OSC title operations are unsupported;
- a `TERM` name SHALL NOT be treated as proof of support;
- successful byte transmission SHALL NOT be reported as proof that the terminal applied the title.

### 7.2 0.4.0 support policy

The semantic title operations SHALL be designed around **emission semantics**, not an emulator-state claim.

The long-term public result shape MAY distinguish:

- endpoint unavailable/inappropriate;
- known unsupported;
- support unknown;
- frame emitted;
- output failure.

T29 does not freeze exact public enum/type names.

### 7.3 Default behavior constraint

A title operation SHALL NOT silently write OSC bytes to an endpoint which the session already knows is not suitable for terminal-control output.

For an interactive or explicitly supplied terminal-control-capable test/backend endpoint where support is otherwise unknown, 0.4.0 MAY permit optimistic emission. Exact public opt-in/default naming is deferred to T35, but the semantic distinction between **unknown support** and **known unsupported** is frozen now.

---

## 8. Public API design constraints

T29 freezes these API-shape rules without freezing final member names:

1. Public APIs SHALL be semantic operations for OSC 0, OSC 1, and OSC 2 behavior.
2. Public callers SHALL NOT supply raw OSC selector numbers.
3. `0.4.0` SHALL NOT expose a general-purpose public `SendOsc(...)` or raw escape-sequence injection API.
4. Title operations SHALL NOT imply that `Icod.Terminal` maintains an authoritative terminal-emulator title state.
5. Exact prior-title restoration SHALL NOT be promised without an independently reliable observation/query mechanism.
6. OSC 0, OSC 1, and OSC 2 intent SHALL remain distinguishable even if convenience naming is later layered over them.
7. Implementation reuse SHALL occur behind internal framing/encoding helpers established by T30.
8. Later OSC 7/8/52 requirements SHALL not be baked into the 0.4.0 public API speculatively.

Provisional semantic names remain examples only:

```text
SetTitle(...)
SetIconName(...)
SetWindowTitle(...)
```

T35 performs the actual public API freeze.

---

## 9. Error and cancellation constraints

T29 freezes the following behavioral constraints for later implementation:

- null arguments at public managed boundaries SHALL follow normal argument-validation policy;
- malformed UTF-16, forbidden controls, and oversized encoded payloads are caller/input errors and SHALL fail before output;
- output failures remain distinguishable from validation failures;
- cancellation before transmission, if an async form is exposed, SHALL emit nothing;
- once transmission begins, the implementation SHALL preserve frame integrity rather than deliberately abandoning a frame mid-sequence in response to ordinary cancellation;
- no title operation requires an input-side response transaction in 0.4.0.

Exact exception/result types remain a T35 decision.

---

## 10. Explicit 0.4.0 non-goals

The following are frozen out of scope:

- OSC 7 current-working-directory publication;
- OSC 8 hyperlinks;
- OSC 52 clipboard/selection;
- arbitrary public OSC emission;
- generic terminal protocol plug-ins;
- title-stack push/pop operations;
- title query/report operations;
- xterm window-management commands;
- terminal-emulator title-state tracking;
- cursor-style operations;
- synchronized output;
- CSI-u or Kitty keyboard protocols;
- BEL as an outbound OSC terminator option;
- 8-bit C1 OSC/ST emission.

---

## 11. T30 implementation handoff

T30 SHALL implement an internal OSC writer capable of producing exactly the three frozen title frames while enforcing this document's validation and size rules.

The first byte-exact fixtures SHALL include at least:

```text
OSC 0 empty
OSC 0 ASCII
OSC 1 ASCII
OSC 2 ASCII
OSC 0 multilingual UTF-8
4096-byte payload boundary
4097-byte rejection
ESC rejection
BEL rejection
NUL rejection
LF/CR/TAB rejection
C1-control rejection
unpaired-surrogate rejection
```

Every rejected case SHALL prove that zero bytes were written.

---

## 12. T29 completion gate

T29 is complete when the repository records the following frozen decisions:

- OSC 0/1/2 selector semantics are sourced to xterm-family documentation;
- generic OSC/ST framing is grounded in ECMA-48;
- outbound framing uses `ESC ]` and `ESC \` exclusively in 0.4.0;
- ST is the sole canonical outbound terminator;
- managed title text uses strict UTF-8 encoding;
- C0, DEL, and C1 control characters are rejected;
- payloads are limited to 4096 encoded UTF-8 bytes;
- validation occurs before any output;
- support uncertainty is not confused with successful emission;
- no generic public OSC API is introduced;
- exact restoration/title querying is not promised;
- later OSC 7/8/52 and other operational protocols remain out of scope.

These decisions are now the implementation contract for T30 through T36 unless a later tranche discovers concrete interoperability evidence requiring an explicit roadmap amendment.
