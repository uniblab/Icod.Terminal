# Compatibility and Versioning

This document defines the permanent compatibility and versioning policy for the `Icod.Terminal` 1.x line.

The public API fingerprint, permanent semantic documentation, package contracts, and downstream acceptance tests together define the supported 1.x contract. Compatibility is not limited to “does it compile?”; ownership, restoration, cancellation, query routing, resource bounds, capability evidence, and security behavior are also part of the contract where documented as guarantees.

## 1. Versioning model

`Icod.Terminal` uses semantic versioning for the public package line.

For stable 1.x releases:

- a **patch** release fixes defects, strengthens tests/documentation, improves performance, or hardens implementation without intentionally breaking the documented 1.x contract;
- a **minor** release may add compatible public APIs, semantic protocol support, or optional behavior while preserving existing public signatures and documented guarantees;
- a **major** release is required for ordinary intentional source/binary breaks, removal or incompatible reinterpretation of public members, enum renumbering, or incompatible changes to documented ownership/security/restoration semantics.

A bug fix may change observed behavior when the previous behavior violated the already-documented contract. Such corrections must still be documented when compatibility-sensitive.

## 2. Machine public API baselines

The stable `1.0.0` exported surface remains frozen by:

- `docs/Public-API-Baseline-1.0.md`;
- `docs/Public-API-Baseline-1.0.sha256`.

Compatible minor-release additions receive separate reviewed baselines rather than overwriting earlier evidence:

- `1.1` records the additive OSC 633 surface;
- `1.2` records the additive OSC 777 titled-notification surface;
- `1.3` records the additive typed iTerm2 OSC 1337 surface;
- `1.4` records the additive typed Kitty OSC 99 notification/query surface;
- `1.7` records the additive backend-neutral raster-display surface.

Versions `1.5.0` and `1.6.0` intentionally added no public API. Their generated snapshots remained identical to 1.4 at:

```text
3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

Version `1.7.0` intentionally advances the current public API fingerprint to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

The authoritative current baseline is:

- `docs/Public-API-Baseline-1.7.md`;
- `docs/Public-API-Baseline-1.7.sha256`.

Historical baselines remain checked in unchanged as compatibility evidence.

`packaging/VerifyPublicApiBaseline.ps1` points at the current release baseline. It regenerates the reflection snapshot independently for `net8.0`, `net9.0`, and `net10.0`, proves the three surfaces agree, and verifies the current fingerprint.

The fingerprint is a review gate, not a declaration that 1.x can never grow. An intentional compatible addition in a minor release requires an explicit new/current baseline in the same reviewed change. Accidental drift must fail CI.

## 3. Intentional 1.7 public additions

Version 1.7 adds these public raster types:

```text
TerminalRasterPixelFormat
TerminalRasterColor
TerminalRasterImage
```

and this additive `TerminalSession` method:

```csharp
ValueTask<TerminalControlMutationResult> DisplayRasterAsync(
	TerminalRasterImage image,
	CancellationToken cancellationToken = default
);
```

`TerminalRasterPixelFormat` currently defines the closed values:

```text
Rgb24    = 0
Rgba32   = 1
Indexed8 = 2
```

These numeric values are part of the 1.x compatibility contract from 1.7 onward.

`TerminalRasterColor` represents straight RGBA8 color. `TerminalRasterImage` represents a bounded immutable-owned snapshot of raw raster data and exposes factories for RGB24, RGBA32, and Indexed8 plus dimensions/format/pixel count and typed color inspection.

The public contract does not expose mutable raster backing memory, a Sixel payload container, raw DCS construction, palette-register controls, placement identifiers, or an explicit Sixel backend selector.

## 4. Source and binary compatibility

Within the stable 1.x line, ordinary releases preserve existing public type/member names and signatures.

The following are compatibility-sensitive and normally require a major release if they affect an existing public contract:

- removing or renaming a public type/member;
- changing parameter order or types;
- changing a return type incompatibly;
- making an optional parameter required;
- tightening nullability in a way that rejects previously valid calls;
- changing implemented public interfaces incompatibly;
- changing public enum numeric values;
- changing public constant values when callers may have compiled them into assemblies;
- changing an existing operation from supported behavior to unconditional failure without an exceptional compatibility reason.

Compatible overloads, new types, and new semantic operations may be introduced in a minor release when they do not make existing behavior ambiguous or unsafe.

## 5. Public enums

Existing public enum numeric values are stable throughout 1.x. An existing value must not be renumbered or reused for another meaning.

Adding an enum value is compatibility-sensitive even when binary-compatible. It requires:

- a minor release;
- explicit compatibility review;
- an intentional public API baseline update;
- documentation of how callers should handle previously unknown values.

This applies to `TerminalRasterPixelFormat` from version 1.7 onward just as it applies to prior stable enums.

## 6. Behavioral compatibility

The permanent documents under `docs/` define behavioral guarantees versioned alongside the API.

Examples include:

- one authoritative live-session input reader;
- query correlation and bounded late-response ownership;
- timeout late-response ownership measured from the logical monotonic deadline rather than scheduler-continuation timing;
- pre-commit versus post-commit cancellation behavior;
- truthful `Unavailable` / `Unsupported` / failure distinctions;
- exact restoration where explicitly promised;
- terminal-policy reset where exact restoration is not claimed;
- session/lease ownership and disposal authority;
- parser/query/raster resource ceilings;
- output serialization boundaries;
- static TermInfo/profile advertisement remaining distinct from generation-scoped live evidence;
- `InvalidateState()` expiring live probe/protocol-response evidence;
- terminal/vendor identity and caller routing preference not being treated as capability evidence;
- query timeout not being automatically converted into unsupported truth;
- committed Sixel output not being truncated by ordinary caller cancellation after the frame commit point;
- transport failure after partial graphics output being surfaced without automatic replay;
- teardown draining committed graphics before output-state restoration.

A minor/patch release may strengthen correctness while preserving these guarantees, but must not silently weaken or reverse them.

## 7. Raster compatibility contract

The public raster model is backend-neutral. Its meaning is not “a Sixel image”; it is bounded raw image data plus semantic display intent.

This distinction is a compatibility promise. Future backends such as Kitty Graphics may implement `DisplayRasterAsync(...)` without requiring callers to change their `TerminalRasterImage` construction.

Version 1.7 implements the semantic operation through Sixel only and preserves these rules:

- the raw raster object owns a snapshot of caller pixel/palette storage;
- RGB24 is opaque;
- RGBA32 and indexed palette colors preserve straight alpha in the model;
- fractional alpha remains valid model data even though Sixel cannot preserve it in 1.7;
- backend inability to preserve fractional alpha yields a controlled unsupported result rather than hidden compositing;
- unknown/unverified graphics support does not cause blind Sixel emission;
- a narrowly scoped Primary DA probe may be used to obtain positive Sixel evidence;
- successful byte emission is not represented as proof that the image was visually displayed.

Adding placement/scaling policy or another raster backend later must preserve the meaning of this existing semantic contract.

## 8. Sixel protocol compatibility

Sixel is an implementation backend below the public raster operation.

Version 1.7 freezes the internal canonical output behavior required by the release tests, including:

- canonical seven-bit DCS framing;
- deterministic palette/quantization behavior;
- bounded dimensions/pixels/palette/work state;
- bounded streaming segments rather than a giant complete-frame allocation;
- caller cancellation before commitment but not frame truncation after commitment;
- session output serialization through final ST and flush;
- no automatic retry after partial transport failure.

These are behavioral/security commitments even though the internal encoder types are not public API.

## 9. Capability evidence and uncertainty

Protocol support and NuGet package compatibility are separate concerns.

A successful semantic output call proves emission, not terminal recognition or visual application unless a protocol supplies explicit acknowledged evidence.

For Sixel in 1.7:

- Primary DA attribute `4` may record `Verified / ProtocolResponse` evidence for `DcsSixel`;
- a valid Primary DA response without `4` remains unknown rather than automatically unsupported;
- a timeout remains unknown;
- caller cancellation is not negative capability evidence;
- terminal name, `TERM`, host OS, or emulator brand is not capability proof.

Live Sixel evidence is generation-scoped and expires under the existing state-invalidation contract.

## 10. Resource-bound compatibility

Documented resource ceilings are part of the safety contract. Implementations may become more efficient, but minor/patch releases must not silently remove bounds and introduce unbounded work/retention.

Version 1.7 raster ceilings include:

```text
maximum dimension       16,384
maximum pixel count      16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette  256 entries
```

The quantizer also uses a fixed `32 x 32 x 32` histogram and the streaming encoder emits bounded segments.

Changing a ceiling may be compatible when it only increases accepted safe input without changing existing semantics, but decreases that reject previously supported values require explicit compatibility review.

## 11. Target frameworks

The stable 1.x contract targets:

```text
net8.0
net9.0
net10.0
```

All three are first-class package targets.

Vendor end-of-support alone is not sufficient reason to remove net8.0 or net9.0. Removal requires a concrete security alert, security-fix incompatibility, runtime/toolchain blocker, or equivalent maintenance constraint preventing responsible support.

Dropping a target framework is compatibility-sensitive and must be documented explicitly.

## 12. Operating-system support

The built-in `SystemTerminalControlProvider` provides native terminal-control behavior for:

- Windows;
- Linux;
- macOS.

Other operating systems receive controlled `Unsupported` results from the built-in provider rather than fabricated POSIX/Windows behavior.

Custom platform/transport implementations remain possible through:

- `ITerminalControlProvider`;
- `ITerminalInput`;
- `ITerminalOutput`.

Sixel output itself is terminal traffic; it does not depend on a host-native graphics API.

## 13. Architecture compatibility

Permanent layer boundaries remain part of the support model:

- `Icod.TermInfo` owns immutable capability information;
- `Icod.Terminal` owns the live terminal conversation, query/evidence model, semantic protocol output, raster output, and reversible session mechanics;
- `Icod.DCurses` owns higher-level virtual-screen/curses presentation policy;
- PTY/process hosting remains orthogonal.

Version 1.7 does not move virtual-screen ownership into `Icod.Terminal`; it provides semantic raster output that higher-level consumers may use.

## 14. Direct consumers and Icod.DCurses

Direct consumers should use `TerminalSession` when they need live terminal/session mechanics without a curses virtual-screen model.

Applications needing windows/cells/diff/refresh should normally use `Icod.DCurses` and allow that layer to own the supplied session according to its integration contract.

Do not create independent state-owning sessions over the same physical terminal merely to divide responsibilities.

## 15. Security compatibility

Security boundaries are compatibility commitments, not optional implementation details.

Stable 1.x does not use a minor/patch release to quietly introduce:

- generic raw OSC/CSI/DCS/APC/vendor dispatch as the ordinary API;
- hazardous host-affecting OSC 9 commands;
- generic raw OSC 633/777/1337/99 dispatch replacing reviewed semantic surfaces;
- a public arbitrary CSI/DCS/Sixel writer merely because internal grammars exist;
- terminal-brand-triggered activation presented as capability truth;
- a competing protocol-specific input reader;
- automatic clipboard reads;
- hidden shell/environment metadata capture;
- automatic image-file decoding or network/process side effects in raster display;
- silent compositing of unsupported fractional-alpha raster data;
- cancellation-driven truncation of already-committed control strings.

New security-sensitive semantic features require explicit typed API, bounded validation, documentation, and tests.

## 16. Deprecation policy

When an existing 1.x API can be replaced compatibly, deprecation is preferred before removal.

Ordinary removal should identify a replacement, document migration, mark the old surface obsolete where practical, preserve it through a reasonable migration interval, and remove it only in a major release.

Exceptional removal without a normal deprecation period is reserved for cases such as active security vulnerability or an impossible-to-support contract and still requires explicit release documentation.

## 17. Compatibility evidence

A release is not considered compatible merely because unit tests pass.

The repository maintains layered evidence including:

- retained historical public API fingerprints plus the current 1.7 fingerprint;
- Windows/Linux/macOS runtime/source validation;
- exact multi-TFM API snapshot agreement;
- fresh NuGet-only consumers for newly added semantic APIs;
- generated XML documentation verification;
- retained historical package consumers/contracts;
- current `Icod.DCurses` package-boundary integration/ownership tests;
- repeated ownership/disposal hardening;
- exact protocol regression vectors;
- resource-bound tests;
- release/distribution validation on configured architectures.

For version 1.7, D178 passed the complete Staging matrix on exact head `f9428927168524be5cc552c82ad00e2fcda70b13`, workflow `34412478452`, including the new public API fingerprint. D179 adds package-only raster/XML qualification and requires a final documentation-complete exact head to pass the same full Staging matrix before the PR leaves draft status.

## 18. Release rule

A green feature checkpoint is not publication authorization.

For `1.7.0`:

1. the exact final D179 PR head must pass the full Staging matrix;
2. the PR may then leave draft status;
3. merge requires explicit authorization/action;
4. the resulting `main` head must pass Release distribution validation;
5. `v1.7.0` tagging/publication requires separate explicit authorization.

These gates may evolve operationally, but equivalent compatibility evidence must exist before historical checks are removed.
