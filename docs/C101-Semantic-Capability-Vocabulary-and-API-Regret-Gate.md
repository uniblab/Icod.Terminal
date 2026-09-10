# C101 — Semantic Capability Vocabulary and API Regret Gate

**Release:** `Icod.Terminal 1.10.0`  
**Development version:** `1.10.0-alpha.1`  
**Status:** design freeze candidate  
**Depends on:** C100 dependency-decoupling and validation hygiene

## Objective

Define the smallest public vocabulary needed for semantic capability inspection and later bounded verification without exposing internal backend identity, protocol framing, routing policy, raw evidence records, terminal-brand heuristics, or dependency-specific implementation details.

The public contract must remain useful if:

- `Icod.TermInfo` changes independently;
- an additional static terminal-description source is introduced later;
- a semantic operation gains or loses protocol backends;
- backend preference order changes;
- a capability moves from static evidence to live verification or vice versa;
- higher-level consumers such as `Icod.DCurses` need to plan behavior without learning terminal protocol mechanics.

## Regret-gate conclusions

C101 freezes the following design choices for C102 implementation.

### 1. Do not publish `TerminalSemanticOperation`

The current internal semantic enum contains 21 implementation-oriented operations. It is an excellent routing vocabulary but is too broad and too coupled to internal layering to become the public planning API unchanged.

Examples of internal distinctions that should remain private include:

- `CurrentLocation` versus `ShellCurrentDirectoryMetadata`;
- `CursorStyle` versus `CursorStyleObservation`;
- `PaletteColor` versus `DynamicColor`;
- `SemanticPromptLifecycle` versus `ShellIntegrationMetadata`;
- output operations whose current implementation intentionally uses a safe fallback even when support evidence is unknown.

Publishing the internal enum would turn future internal refactoring into public compatibility work.

### 2. Publish a curated semantic capability enum

The initial 1.10 public inspection vocabulary should contain only capabilities for which higher-level software has a credible planning decision to make and for which the current session can report useful support knowledge.

Freeze this initial set:

```text
ClipboardRead
ClipboardWrite
CursorStyle
SynchronizedOutput
KeyboardReporting
MouseReporting
FocusReporting
BracketedPaste
RasterGraphics
```

Proposed public type name:

```csharp
TerminalCapability
```

Rationale:

- clipboard read/write differ materially in endpoint and query requirements;
- cursor style is a reversible presentation capability with useful static evidence;
- synchronized output materially changes refresh planning;
- keyboard, mouse, focus, and bracketed-paste reporting materially affect input-mode planning;
- raster graphics materially affects whether a higher-level consumer can choose a raster path.

Do **not** add public capability values in 1.10 merely because an internal semantic operation exists.

### 3. Keep output conveniences out of the initial capability enum

The following internal operations are intentionally excluded from the initial public capability vocabulary:

```text
TerminalTitle
DesktopNotification
CurrentLocation
ShellCurrentDirectoryMetadata
Hyperlink
CursorStyleObservation
TerminalProgress
PointerShape
PaletteColor
DynamicColor
SemanticPromptLifecycle
ShellIntegrationMetadata
```

This is not a declaration that they can never be inspected. They are excluded because at least one of these is currently true:

- the operation has a deliberately safe output fallback and a binary support answer would overstate what is known;
- the current internal semantic operation combines multiple public-use cases that deserve a better public abstraction first;
- the planning value for higher-level consumers is weak compared with the compatibility cost of freezing a new enum member;
- verification semantics are not yet clean enough to promise a stable public contract.

Future minor releases may add public `TerminalCapability` values additively after the same regret gate.

## Public state model

Capability support, endpoint availability, and evidence provenance are distinct concepts and must remain separate in the public model.

### Support state

Proposed public type:

```csharp
TerminalCapabilitySupport
```

Freeze these values and meanings:

```text
Unknown
    The session does not currently have decisive support evidence.

Unsupported
    Current evidence establishes that the semantic capability is not supported.

Advertised
    Static terminal-description evidence advertises usable support, but the current
    lifecycle generation has not strengthened that answer through live observation.

Verified
    Current live observation establishes usable support for this lifecycle generation.
```

`Unavailable` is deliberately **not** a support-state value.

### Endpoint availability

Proposed public type:

```csharp
TerminalCapabilityEndpointAvailability
```

Freeze these values:

```text
Available
Unavailable
```

Endpoint unavailability means the capability cannot presently be used through this `TerminalSession` because the required terminal endpoint is unavailable. It does **not** mean the terminal lacks support.

Examples:

```text
RasterGraphics + output redirected
    EndpointAvailability = Unavailable
    Support may remain Unknown/Advertised/Verified internally, but current usability is false.

KeyboardReporting + non-interactive input
    EndpointAvailability = Unavailable
    This is not evidence that the terminal protocol is unsupported.
```

The C102 result object may expose a convenience `IsUsable` property derived from endpoint availability plus support/routing knowledge, but endpoint state itself remains explicit.

## Public evidence model

The public API must not expose internal evidence-source identities directly.

The current internal sources are:

```text
TermInfo
BuiltInProfile
LiveProbe
ProtocolResponse
```

Publishing those names would couple consumers to current dependency and protocol implementation.

Proposed public type:

```csharp
TerminalCapabilityEvidenceKind
```

Freeze these values:

```text
None
StaticDescription
LiveObservation
```

Projection rule:

```text
internal TermInfo       -> StaticDescription
internal BuiltInProfile -> StaticDescription
internal LiveProbe      -> LiveObservation
internal ProtocolResponse -> LiveObservation
no decisive evidence    -> None
```

This projection is intentionally lossy. Consumers need to know the strength/lifetime class of evidence, not which package or control protocol produced it.

## Result-shape direction

C102 should implement one immutable public result carrying at least:

```text
Capability
Support
EndpointAvailability
EvidenceKind
IsUsable
```

Proposed type name:

```csharp
TerminalCapabilityStatus
```

The result must not contain:

```text
TerminalProtocolBackend
TerminalBackendSelectionReason
TerminalCapabilityEvidenceSource
raw capability names
raw query bytes
TERM/terminal brand
Icod.TermInfo types or identifiers
routing preference values
```

## Capability mapping

C102/C103 should use an explicit private mapping rather than enum numeric casts.

Freeze this mapping:

```text
TerminalCapability.ClipboardRead
    -> TerminalSemanticOperation.ClipboardRead

TerminalCapability.ClipboardWrite
    -> TerminalSemanticOperation.ClipboardWrite

TerminalCapability.CursorStyle
    -> TerminalSemanticOperation.CursorStyle

TerminalCapability.SynchronizedOutput
    -> TerminalSemanticOperation.SynchronizedOutput

TerminalCapability.KeyboardReporting
    -> TerminalSemanticOperation.KeyboardReporting

TerminalCapability.MouseReporting
    -> TerminalSemanticOperation.MouseReporting

TerminalCapability.FocusReporting
    -> TerminalSemanticOperation.FocusReporting

TerminalCapability.BracketedPaste
    -> TerminalSemanticOperation.BracketedPaste

TerminalCapability.RasterGraphics
    -> TerminalSemanticOperation.RasterGraphics
```

The mapping is private so internal semantic-operation ordering and future internal decomposition remain free to change.

## Usability semantics

C101 freezes an important distinction between support evidence and route usability.

A capability is presently usable only when:

1. its required endpoint is available; and
2. the existing semantic resolver can select a usable route under current evidence/policy.

Therefore:

```text
Verified + Available       -> usable
Advertised + Available     -> usable when the current resolver selects the advertised route
Unknown + Available        -> may be usable only where current routing policy has an intentional safe fallback
Unsupported + Available    -> not usable
Any support + Unavailable  -> not presently usable
```

The public API must project the existing resolver result rather than invent a second capability policy engine.

## Inspection versus verification

C101 freezes terminology:

```text
Inspect
    Side-effect free.
    Reads existing session knowledge and routing state only.

Verify
    Explicit asynchronous operation.
    May perform a bounded live probe when a reviewed probe exists.
```

Do not use `Detect`, `Discover`, or `Probe` as the primary public verb in 1.10. Those words invite protocol-oriented interpretation and can obscure whether bytes are emitted.

C102 implements inspection only. C104 owns verification.

## Lifecycle semantics

Static-description evidence survives lifecycle-generation changes.

Live-observation evidence remains generation-scoped and may cease to be decisive after suspend/resume or another existing live-generation advance.

The public `EvidenceKind` reports the evidence currently effective for the returned status. It does not expose generation numbers.

Generation identity remains internal unless a future concrete consumer requirement demonstrates value.

## Extensibility decision

Use public enums for the initial capability, support, endpoint-availability, and evidence-kind vocabularies.

Reasons:

- the curated set is intentionally small;
- the values are semantic rather than protocol-specific;
- additive enum members are appropriate for future minor versions under the existing 1.x compatibility policy;
- opaque string identifiers would move typo/error handling to consumers and weaken API discoverability;
- arbitrary capability names would recreate the raw-capability escape hatch this release is intended to avoid.

Consumers must continue to tolerate future enum values according to normal forward-compatible .NET guidance; library code must validate incoming enum parameters at public method boundaries.

## Explicit non-goals

C101 does not authorize:

- publishing the internal 21-value semantic-operation enum;
- publishing protocol/backend names;
- exposing raw `Icod.TermInfo` capability names;
- exposing `Icod.TermInfo` as a public evidence-source enum value;
- terminal-brand capability heuristics;
- generic string capability lookup;
- hidden live probes during inspection;
- a second terminal reader;
- persistent capability caches outside the existing session/lifecycle model.

## C102 entry criteria

C102 may begin when the following are treated as frozen for the first implementation:

```text
public capability vocabulary: 9 curated values
support state: Unknown / Unsupported / Advertised / Verified
endpoint availability: Available / Unavailable
evidence kind: None / StaticDescription / LiveObservation
result direction: TerminalCapabilityStatus
inspection verb: InspectCapability(...)
internal mapping: explicit, private, non-numeric
```

Any expansion of these decisions before C102 acceptance should update this record and explain why the smaller contract was insufficient.
