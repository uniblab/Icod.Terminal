# N156 — Semantic Backend Registry

**Release:** `1.5.0`  
**Status:** implemented; exact-head validation pending  
**Scope:** internal normalization infrastructure

## Purpose

N156 records which concrete implementation backends are legitimate candidates for each normalized semantic terminal operation.

The registry answers only:

> Which reviewed implementations can satisfy this semantic intent?

It does **not** answer:

- which backend is currently supported;
- which backend should be preferred;
- whether a caller explicitly requested one backend;
- whether the terminal endpoint is currently available.

Those questions belong to N155 evidence and N157 routing policy.

## Complete semantic coverage

Every `TerminalSemanticOperation` value must have one explicit registry entry with at least one unique candidate. Registry construction fails if a semantic operation is omitted.

This prevents future enum growth from silently creating an unroutable semantic operation.

## Candidate identity

Each `TerminalSemanticBackendCandidate` retains:

```text
Semantic operation
Protocol backend
Control family (when applicable)
```

`TermInfoCapability` deliberately has no raw control-family classification because it represents an already selected/expanded terminal capability recipe rather than one hard-coded control-language backend.

## Registry versus routing preference

Candidate declaration order is deterministic for inspection and testing, but it is **not routing preference**.

For example, the notification registry contains:

```text
OSC 9
OSC 777
OSC 99
```

This does not mean OSC 9 is preferred. N157 owns the explicit reviewed preference table and combines it with N155 evidence.

## Reviewed mappings

### Terminal title

```text
OSC 0
OSC 2
```

### Desktop notification

```text
OSC 9
OSC 777
OSC 99
```

### Current location

```text
OSC 7
OSC 9;9 Windows compatibility
```

Vendor shell-integration current-directory metadata is intentionally excluded from this semantic operation.

### Shell current-directory metadata

```text
OSC 633 Cwd
OSC 1337 CurrentDir
```

### Hyperlink

```text
OSC 8
```

### Clipboard write

```text
exact TermInfo capability
OSC 52
```

### Clipboard read

```text
OSC 52
```

TermInfo is not listed for clipboard reads because an output capability recipe does not imply an active clipboard-read/query contract.

### Cursor style

```text
exact TermInfo capability
DECSCUSR CSI
```

### Cursor-style observation

```text
DECRQSS DCS
```

### Synchronized output

```text
CSI DEC private mode 2026 backend
```

N156 gives this a semantic-specific backend identity rather than registering the generic `CsiDecPrivateMode` mechanism.

### Progress

```text
OSC 9;4
```

### Pointer shape

```text
OSC 22
```

### Palette color

```text
exact TermInfo capability
OSC 4
```

### Dynamic terminal-resource color

```text
OSC 10–14 / 17 / 19 family
```

### Semantic prompt lifecycle

```text
OSC 133
OSC 633
```

### Shell-integration metadata

```text
OSC 633
OSC 1337
```

### Keyboard reporting

```text
Kitty keyboard CSI
xterm modifyOtherKeys CSI
```

### Mouse reporting

```text
semantic-specific CSI mode backend
```

### Focus reporting

```text
semantic-specific CSI mode backend
```

### Bracketed paste

```text
semantic-specific CSI mode backend
```

### Raster graphics

```text
Sixel DCS
Kitty Graphics APC
```

The graphics entry permanently records the family distinction required by the 1.7/1.8 roadmap.

## Generic mechanisms are not semantic candidates

`CsiDecPrivateMode` remains in the protocol vocabulary because it is a useful mechanism classification, but N156 deliberately does not register it as the backend for synchronized output, mouse reporting, focus reporting, or bracketed paste.

Those operations have distinct semantic backend identities so N157 can reason about support and policy without conflating unrelated DEC private modes.

## Relationship to N155

Registry membership creates no capability evidence.

A newly created `TerminalCapabilityEvidenceLedger` resolves every registered backend as `Unknown` until TermInfo/profile/live evidence is supplied.

## Acceptance coverage

N156 tests verify:

- every semantic operation has at least one candidate;
- each operation's candidates are unique;
- candidate control-family classification matches N150 vocabulary;
- notification routing candidates include all three current notification protocols;
- portable current location remains separate from vendor shell metadata;
- clipboard write may use TermInfo while clipboard read remains OSC 52 only;
- cursor and palette operations reserve exact TermInfo candidate slots;
- Sixel remains DCS and Kitty Graphics remains APC;
- mode-based semantics use specific backend identities rather than generic `CsiDecPrivateMode`;
- registry declaration alone creates no N155 support evidence;
- invalid semantic operation identifiers are rejected.

N156 introduces no public API and changes no terminal wire behavior.
