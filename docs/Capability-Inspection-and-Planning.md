# Capability Inspection and Planning

This document is the permanent 1.x authority for public semantic capability planning in `Icod.Terminal`.

The public model deliberately describes **what a live `TerminalSession` currently knows about a semantic operation**. It does not expose protocol-backend selection, routing preference scores, raw terminal capability names, emulator branding, or dependency-specific implementation detail.

## 1. Public vocabulary

Version 1.10 introduces the curated semantic capability set:

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

This enum is intentionally smaller than the internal semantic-routing vocabulary. Internal operations are not automatically public capabilities merely because the implementation can route or emit them.

## 2. Status dimensions

`TerminalCapabilityStatus` keeps four questions separate:

```text
Capability
Support
EndpointAvailability
EvidenceKind
```

and derives:

```text
IsUsable
```

### Support

```text
Unknown
Unsupported
Advertised
Verified
```

`Unknown` means the session does not currently have decisive support knowledge.

`Unsupported` is a reviewed negative result. Silence or probe timeout is not automatically equivalent to `Unsupported`.

`Advertised` means static terminal-description knowledge indicates support but the session does not currently have stronger live confirmation.

`Verified` means current generation-scoped live evidence verifies support.

### Endpoint availability

```text
Unavailable
Available
```

Endpoint availability is not support evidence. A capability may remain `Advertised` or `Verified` while the endpoint required to use it is unavailable in the current session.

For example, redirected output can make a known output capability unusable without rewriting truthful support knowledge to `Unsupported`.

### Evidence kind

```text
None
StaticDescription
LiveObservation
```

The public model intentionally does not distinguish whether static description data came from `Icod.TermInfo`, a built-in profile, or another internal description source. Likewise, callers do not receive raw query/protocol identities as public evidence categories.

`StaticDescription` represents immutable session description/profile knowledge.

`LiveObservation` represents current-generation live protocol/query observation.

## 3. Side-effect-free inspection

Use:

```csharp
TerminalCapabilityStatus status = session.InspectCapability(
    TerminalCapability.RasterGraphics
);
```

Inspection is synchronous and side-effect free. It:

- validates the semantic capability;
- reads the session's existing description/evidence/routing state;
- emits no terminal bytes;
- performs no hidden query or live probe;
- does not change terminal modes or presentation state;
- does not introduce another input reader.

Inspection is therefore appropriate for ordinary UI/application planning.

## 4. Explicit bounded verification

Use:

```csharp
TerminalCapabilityStatus status = await session.VerifyCapabilityAsync(
    TerminalCapability.RasterGraphics,
    cancellationToken
);
```

Verification is explicit because it may emit terminal query traffic.

Version 1.10 performs live verification only where an already-reviewed bounded support probe exists:

```text
KeyboardReporting
RasterGraphics
```

For capabilities without a reviewed support probe, verification returns the current inspection result unchanged. The library does not invent protocol traffic merely to make every enum value probeable.

Verification also short-circuits without probe traffic when:

- the required endpoint is unavailable;
- current live evidence is already decisively `Verified`;
- current live evidence is already decisively `Unsupported`;
- caller cancellation is already requested.

Live verification reuses the existing authoritative query/input coordinator. It does not create a second reader or a generic raw-query escape hatch.

## 5. Planning pattern

Prefer semantic planning:

```csharp
TerminalCapabilityStatus raster = session.InspectCapability(
    TerminalCapability.RasterGraphics
);

if ( raster.IsUsable ) {
    // Offer/use the raster presentation path.
} else {
    // Use a non-raster presentation path.
}
```

When stronger knowledge is worth terminal traffic, make that choice explicit:

```csharp
if ( raster.Support == TerminalCapabilitySupport.Unknown
    && raster.EndpointAvailability
        == TerminalCapabilityEndpointAvailability.Available ) {
    raster = await session.VerifyCapabilityAsync(
        TerminalCapability.RasterGraphics,
        cancellationToken
    );
}
```

Do not branch application policy on terminal brand, `TERM`, host operating system, Kitty-vs-Sixel identity, or internal backend preference.

## 6. Lifecycle and evidence generations

Static terminal-description evidence survives lifecycle generation changes.

Live observation is generation-scoped. Session invalidation and resume advance the live evidence generation so previously observed protocol behavior is no longer treated as current truth.

Consequences:

```text
static Advertised
    + live Verified
        -> current Verified

session invalidation/resume
        -> stale live evidence expires
        -> static Advertised remains
```

Already returned `TerminalCapabilityStatus` objects are immutable snapshots. They do not mutate in place when the session evidence generation changes. Call `InspectCapability(...)` again to obtain current knowledge.

## 7. Query ownership, cancellation, and disposal

Verification obeys the normal session query lifecycle.

- suspended query ownership is not bypassed;
- closed/disposed query ownership is not reopened;
- caller cancellation is honored;
- bounded probe timeout remains uncertainty unless the underlying reviewed protocol provides authoritative negative evidence;
- disposal remains final cleanup authority for in-flight session/query work.

Inspection remains a read of current in-memory session knowledge and does not acquire query ownership.

## 8. Concurrency

Concurrent side-effect-free inspection is permitted and must not emit terminal traffic.

Live verification is serialized/coordinated through the existing session query machinery. Callers should not create competing raw input readers or attempt to route probe responses themselves.

Repeated verification of already-decisive current live evidence does not intentionally re-probe merely to produce the same answer.

## 9. Truthful uncertainty

Capability planning follows the same uncertainty rule as the rest of `Icod.Terminal`:

> lack of proof is not proof of lack.

A timeout, malformed unrelated frame, unsupported query path, or unavailable endpoint must not be silently converted into permanent terminal-wide unsupported truth.

`Unsupported` is reserved for reviewed negative evidence. `Unknown` remains a useful and intentional planning state.

## 10. Dependency neutrality

`Icod.Terminal` uses `Icod.TermInfo` internally for terminal description/capability information, but the public planning API does not expose `Icod.TermInfo` as an evidence identity.

The public distinction is semantic:

```text
StaticDescription
LiveObservation
```

not implementation-specific:

```text
TermInfo
OSC
CSI
DCS
APC
Kitty
Sixel
```

This lets higher-level consumers such as `Icod.DCurses` plan from stable semantic knowledge without coupling themselves to the implementation source of that knowledge.

## 11. Security and trust

Live terminal responses are untrusted terminal-controlled input. Verification inherits the existing query/parser validation, resource bounds, correlation, and malformed-response recovery rules.

A live `Verified` result means the reviewed terminal protocol observation established support under the library's current contract. It is not an authentication statement about the terminal, emulator, desktop session, or user.

## 12. Deliberate exclusions

Version 1.10 does not expose:

- the internal semantic-backend registry;
- backend preference scores;
- explicit Kitty/Sixel/backend selection;
- raw evidence ledger entries;
- raw protocol frames or matcher APIs;
- arbitrary terminfo capability names;
- `Icod.TermInfo` objects as public planning results;
- terminal-brand heuristics;
- hidden/background probing;
- a requirement that every semantic capability have a live probe.

These exclusions preserve the ability to evolve routing and evidence internals without turning them into public compatibility obligations.
