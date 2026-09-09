# N155 — Capability Support and Evidence Model

**Release:** `1.5.0`  
**Status:** implemented; exact-head validation pending  
**Scope:** internal normalization infrastructure

## Purpose

N155 separates **what support state is currently effective** from **where the supporting claim came from**.

This avoids several unsafe shortcuts:

- terminal identity is not itself proof that a protocol works;
- a TermInfo/profile advertisement is weaker than a successful live protocol result;
- a query timeout is not equivalent to an explicit negative response;
- caller routing preference is not capability evidence;
- endpoint unavailability is not a protocol observation.

The model remains internal in 1.5 while N156/N157 build the semantic backend registry and routing resolver on top of it.

## Capability subjects

Evidence may describe either:

```text
SemanticOperation
ProtocolBackend
```

This distinction is intentional.

For example, an exact TermInfo capability may advertise the semantic ability to write the clipboard without implying that the hard-coded OSC 52 backend is the selected implementation. Conversely, a live OSC 99 probe can verify one concrete notification backend.

`TerminalCapabilitySubject` therefore identifies exactly one of:

- `TerminalSemanticOperation`; or
- `TerminalProtocolBackend`.

A default/invalid subject cannot enter the evidence ledger.

## Support states

The N150 support-state vocabulary is retained:

```text
Unavailable
Unsupported
Unknown
Advertised
Verified
```

### Unavailable

`Unavailable` is an **effective endpoint state**. It is not stored as capability evidence.

Examples include a session whose input/output endpoints cannot support the required conversation.

Endpoint unavailability overrides every stored capability observation for the current resolution.

### Unsupported

`Unsupported` requires decisive live evidence, such as an explicit negative protocol result or a live probe with defined negative semantics.

Absence from a TermInfo/profile description is not automatically `Unsupported`.

### Unknown

`Unknown` means there is no decisive current conclusion.

A timeout or inconclusive live probe may produce `Unknown`, but this does not erase an already decisive live result in the same live generation.

### Advertised

`Advertised` is static evidence from:

- `TermInfo`; or
- `BuiltInProfile`.

It means the backend/operation is described as available, not that the current endpoint has been live-verified.

### Verified

`Verified` requires decisive live evidence from:

- `LiveProbe`; or
- `ProtocolResponse`.

## Evidence sources

The N150 evidence-source vocabulary remains:

```text
TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

Caller routing policy does not appear in this list. Preference influences backend selection later; it cannot manufacture support truth.

## Valid source/state combinations

N155 deliberately restricts what each source may record.

### Static sources

`TermInfo` and `BuiltInProfile` may record only:

```text
Unknown
Advertised
```

They may not claim live verification or a decisive runtime negative result.

### Live sources

`LiveProbe` and `ProtocolResponse` may record only:

```text
Unknown
Unsupported
Verified
```

They may not downgrade themselves to mere static advertisement.

## Resolution precedence

For one subject with an available endpoint, effective state is resolved in this order:

```text
latest decisive live evidence
    ↓
TermInfo advertisement
    ↓
built-in profile advertisement
    ↓
latest inconclusive live observation
    ↓
Unknown
```

If the endpoint is unavailable, `Unavailable` wins before stored evidence is considered.

### Decisive live conflicts

When live sources disagree, the **most recently recorded decisive live observation** wins.

This permits legitimate state changes:

```text
Unsupported -> later Verified
Verified    -> later Unsupported
```

without assigning permanent authority to one live source category.

### Unknown does not erase decisive live evidence

An inconclusive probe after a successful verification does not convert the backend to `Unknown`.

Likewise, an inconclusive live observation does not override a static advertisement when no decisive live result exists.

This is the key rule that prevents transient timeout/noise from becoming false unsupported or from erasing stronger evidence.

## Lifecycle generations

Live evidence is generation-scoped.

`TerminalCapabilityEvidenceLedger.AdvanceLiveGeneration()`:

- increments the live generation;
- invalidates all `LiveProbe` and `ProtocolResponse` observations;
- preserves `TermInfo` and `BuiltInProfile` evidence.

This is the contract later session lifecycle integration will consume when a suspend/resume or equivalent terminal-context transition makes old live observations unsafe to reuse.

Static evidence survives because the selected terminal description/profile itself has not changed merely because the live endpoint was suspended.

## Boundedness

The ledger is bounded by the finite set of valid semantic-operation and protocol-backend enum subjects. Each subject retains only:

- the latest TermInfo state;
- the latest built-in profile state;
- the latest decisive live-probe state;
- the latest decisive protocol-response state;
- the latest inconclusive live-probe state;
- the latest inconclusive protocol-response state.

It does not retain an unbounded history of observations.

## Relationship to N156/N157

N155 does **not** select a backend.

N156 will define reviewed candidate backends for each semantic operation. N157 will combine:

- endpoint availability;
- N155 effective evidence;
- registry ordering/compatibility;
- caller routing policy;

without conflating any of those concepts.

## Acceptance coverage

N155 tests verify:

- endpoint unavailability overrides verified evidence;
- TermInfo advertisement outranks built-in profile advertisement;
- verified live evidence overrides static advertisement;
- explicit negative live evidence overrides static advertisement;
- later decisive live evidence supersedes earlier decisive live evidence;
- `Unknown` does not erase decisive live evidence;
- `Unknown` does not override stronger static advertisement;
- inconclusive live evidence is retained when no stronger evidence exists;
- live-generation advancement discards live evidence but preserves static advertisement;
- subjects remain isolated;
- invalid source/state combinations are rejected;
- invalid/default capability subjects cannot enter the ledger.

N155 introduces no public API and no terminal wire behavior.
