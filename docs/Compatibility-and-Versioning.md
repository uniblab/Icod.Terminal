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
- `1.7` records the additive backend-neutral raster-display surface;
- `1.9` records the additive protocol-neutral semantic-event envelope and interactive Kitty notification options.

Versions `1.5.0` and `1.6.0` intentionally added no public API. Their generated snapshots remained identical to 1.4 at:

```text
3654594768a0e47be7c43820bef96779739e12ce4b710d4eaca43308bef86b27
```

Version `1.7.0` intentionally advanced the public API fingerprint to:

```text
847441fb4a8cdc89979aca9e96178f939895b93ec19a973232210af09716f700
```

Versions `1.8.0` and `1.8.1` intentionally added no public API and retained that exact fingerprint. There is therefore no redundant `Public-API-Baseline-1.8` file.

Version `1.9.0` intentionally advances the current public API fingerprint to:

```text
e652e6fd65cd43422ca84b7c4c2a1815ee7ead9b2a64285e0e17cf39614b0315
```

The authoritative current baseline is:

- `docs/Public-API-Baseline-1.9.md`;
- `docs/Public-API-Baseline-1.9.sha256`.

Historical baselines remain checked in unchanged as compatibility evidence.

`packaging/VerifyPublicApiBaseline.ps1` points at the current baseline. It regenerates the reflection snapshot independently for `net8.0`, `net9.0`, and `net10.0`, proves the three surfaces agree, and verifies the current fingerprint.

The fingerprint is a review gate, not a declaration that 1.x can never grow. An intentional compatible addition in a future minor release requires an explicit new/current baseline in the same reviewed change. Accidental drift must fail CI.

## 3. Intentional 1.7 public raster additions

Version 1.7 added these public raster types:

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

`TerminalRasterPixelFormat` defines the closed values:

```text
Rgb24    = 0
Rgba32   = 1
Indexed8 = 2
```

These numeric values are part of the 1.x compatibility contract from 1.7 onward.

`TerminalRasterColor` represents straight RGBA8 color. `TerminalRasterImage` represents a bounded immutable-owned snapshot of raw raster data and exposes factories for RGB24, RGBA32, and Indexed8 plus dimensions/format/pixel count and typed color inspection.

The public contract does not expose mutable raster backing memory, a Sixel or Kitty payload container, raw DCS/APC construction, palette-register controls, placement identifiers, or an explicit raster backend selector.

## 4. Intentional 1.8 backend expansion with no public API delta

Version 1.8 is a compatibility demonstration of the backend-neutral 1.7 design.

The same public call:

```text
TerminalSession.DisplayRasterAsync(...)
```

may now be implemented internally by either reviewed backend:

```text
verified ApcKittyGraphics
verified DcsSixel
```

Verified Kitty Graphics is preferred and verified Sixel remains fallback. The caller does not opt into Kitty through a new public overload or construct Kitty control data.

This is an implementation/semantic expansion below an existing public contract, not a reinterpretation of `TerminalRasterImage` as a protocol-specific object.

Version 1.8 therefore deliberately adds no public:

- APC or Kitty Graphics writer;
- Sixel/Kitty backend selector;
- persistent image or placement id;
- placement/scaling option;
- source rectangle or z-order model;
- graphics deletion/animation API;
- cursor-normalization graphics option.

Those concepts require separate compatibility review if introduced later.

## 5. Source and binary compatibility

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

## 6. Public enums

Existing public enum numeric values are stable throughout 1.x. An existing value must not be renumbered or reused for another meaning.

Adding an enum value is compatibility-sensitive even when binary-compatible. It requires:

- a minor release;
- explicit compatibility review;
- an intentional public API baseline update;
- documentation of how callers should handle previously unknown values.

Version 1.9 applies this rule to `TerminalEventKind`: `Semantic = 4` is appended after the existing `Input = 0`, `Lifecycle = 1`, `Timeout = 2`, and `Cancelled = 3` values. Consumers with exhaustive switches should handle unknown/new values appropriately when adopting a newer minor release.

This rule also applies to `TerminalRasterPixelFormat` from version 1.7 onward and to every other stable public enum.

## 7. Behavioral compatibility

The permanent documents under `docs/` define behavioral guarantees versioned alongside the API.

Examples include:

- one authoritative live-session input reader;
- active query response ownership preceding unsolicited semantic-event recognition, which in turn precedes ordinary input decoding;
- no double delivery of one frame as both query response and semantic event;
- bounded semantic-event buffering in the same application-event ordering domain as ordinary input;
- malformed/oversized owned semantic reports recovering without leaking hostile bytes into ordinary text and re-entering query precedence before later traffic is decoded;
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
- committed graphics output not being truncated by ordinary caller cancellation after its commit point;
- transport failure after partial graphics output being surfaced without automatic replay/backend switching;
- teardown draining committed graphics before output-state restoration;
- correlated terminal responses remaining untrusted and bounded even after ownership is established.

A minor/patch release may strengthen correctness while preserving these guarantees, but must not silently weaken or reverse them.

## 8. Raster compatibility contract

The public raster model is backend-neutral. Its meaning is not “a Sixel image” or “a Kitty image”; it is bounded raw image data plus semantic display intent.

The raw raster object owns a snapshot of caller pixel/palette storage. RGB24 is opaque. RGBA32 and indexed palette colors preserve straight alpha in the model.

Version 1.7 implemented the semantic operation through Sixel. Version 1.8 adds Kitty Graphics while preserving the meaning of the existing model:

- fractional alpha remains valid common raster data;
- Kitty RGBA32 can preserve fractional alpha;
- Sixel still returns controlled unsupported when it cannot preserve fractional alpha, rather than silently compositing;
- source width/height remain intrinsic raster pixel dimensions;
- unknown/unverified graphics support does not cause blind graphics emission;
- successful byte emission is not represented as proof that the image was visually displayed.

Adding placement/scaling policy or another raster backend later must preserve the meaning of this existing semantic contract.

## 9. Sixel protocol compatibility

Sixel remains an implementation backend below the public raster operation.

Version 1.7 froze the internal canonical behavior required by the release tests, including:

- canonical seven-bit DCS framing;
- deterministic palette/quantization behavior;
- bounded dimensions/pixels/palette/work state;
- bounded streaming segments rather than a giant complete-frame allocation;
- caller cancellation before commitment but not frame truncation after commitment;
- session output serialization through final ST and flush;
- no automatic retry after partial transport failure.

Version 1.8 retains those Sixel behaviors and uses the backend as verified fallback. Adding Kitty Graphics does not change established Sixel bytes or weaken its transaction semantics.

## 10. Kitty Graphics protocol compatibility

Kitty Graphics is an internal APC dialect below the same public raster operation.

Version 1.8 freezes the common-raster subset required by its release tests:

- canonical library output uses seven-bit APC framing;
- direct transmission (`t=d`) only;
- RGB24 (`f=24`) and RGBA32 (`f=32`) raw transmission;
- deterministic Indexed8 expansion to RGB24/RGBA32 according to referenced alpha;
- encoded Base64 image data bounded to 4096 bytes per protocol chunk;
- one logical multi-frame transfer serialized through the existing session output gate;
- caller cancellation before commitment but no ordinary post-commit truncation;
- no automatic replay or Sixel switch after partial committed failure;
- protocol-defined correlated support query plus Primary DA barrier;
- bounded correlated-response ownership and malformed/oversized recovery;
- no public generic Kitty/APC dispatcher.

File/temp-file/shared-memory transport and advanced placement/image lifecycle remain outside the 1.8 compatibility promise.

## 11. Capability evidence and uncertainty

Protocol support and NuGet package compatibility are separate concerns.

A successful semantic output call proves emission, not terminal recognition or visual application unless a protocol supplies explicit acknowledged evidence.

For Sixel:

- Primary DA attribute `4` may record `Verified / ProtocolResponse` evidence for `DcsSixel`;
- a valid Primary DA response without `4` remains unknown rather than automatically unsupported;
- timeout remains unknown;
- caller cancellation is not negative capability evidence.

For Kitty Graphics:

- a valid response correlated by the active probe image id records `Verified / ProtocolResponse`;
- the reviewed Primary DA barrier arriving first records `Unsupported / ProtocolResponse` for `ApcKittyGraphics`;
- timeout before either authoritative result remains unknown;
- caller cancellation is not negative capability evidence;
- terminal name, `TERM`, host OS, or emulator brand is not capability proof.

Live graphics evidence is generation-scoped and expires under the existing state-invalidation contract.

## 12. Correlation and response ownership compatibility

The one-reader/query ownership model is a stable 1.x behavioral contract.

Version 1.8 extends it with a side-observed Kitty probe response without creating another reader. Once a complete matching `i=<probe-id>` field is observed in a recognizable APC prefix, the response remains transaction-owned through later malformed/aborted/oversized recovery.

Version 1.9 extends the same ownership model to unsolicited semantic reports. Active query ownership remains first. A recognized semantic candidate is never used to satisfy an unrelated query and is not leaked into ordinary application text merely because its later metadata, payload, framing, or size is invalid. After bounded semantic recovery, routing restarts at query precedence before later buffered traffic is considered.

This is a hardening of ownership, not a relaxation of trust: correlated or semantically recognizable data still must satisfy grammar and size rules. A matching identifier cannot turn invalid terminal traffic into trusted data.

An identified but unterminated reply is malformed rather than indistinguishable from silence. Unrelated control traffic remains unrelated input/control traffic.

## 13. Resource-bound compatibility

Documented resource ceilings are part of the safety contract. Implementations may become more efficient, but minor/patch releases must not silently remove bounds and introduce unbounded work/retention.

Raster ceilings include:

```text
maximum dimension       16,384
maximum pixel count      16 Mi
maximum owned pixel data 64 MiB
maximum indexed palette  256 entries
```

Additional protocol bounds include the 4096-byte normal response frame, 4096-byte Kitty Base64 image-data chunk, bounded small control-family frames, fixed Sixel histogram, and bounded resynchronization state.

Interactive Kitty notification buttons are additionally bounded in 1.9 to 16 labels, 512 UTF-8 bytes per label, and 2,048 UTF-8 bytes for the combined protocol button payload including separators.

Changing a ceiling may be compatible when it only increases accepted safe input without changing existing semantics, but decreases that reject previously supported values require explicit compatibility review.

## 14. Target frameworks

The stable 1.x contract targets:

```text
net8.0
net9.0
net10.0
```

All three are first-class package targets.

Vendor end-of-support alone is not sufficient reason to remove net8.0 or net9.0. Removal requires a concrete security alert, security-fix incompatibility, runtime/toolchain blocker, or equivalent maintenance constraint preventing responsible support.

Dropping a target framework is compatibility-sensitive and must be documented explicitly.

## 15. Operating-system support

The built-in `SystemTerminalControlProvider` provides native terminal-control behavior for:

- Windows;
- Linux;
- macOS.

Other operating systems receive controlled `Unsupported` results from the built-in provider rather than fabricated POSIX/Windows behavior.

Custom platform/transport implementations remain possible through:

- `ITerminalControlProvider`;
- `ITerminalInput`;
- `ITerminalOutput`.

Sixel and Kitty Graphics output are terminal traffic; neither depends on a host-native graphics API.

## 16. Architecture compatibility

Permanent layer boundaries remain part of the support model:

- `Icod.TermInfo` owns immutable capability information;
- `Icod.Terminal` owns the live terminal conversation, query/evidence model, unsolicited semantic-event routing, semantic protocol output, raster output, backend routing, and reversible session mechanics;
- `Icod.DCurses` owns higher-level virtual-screen/curses presentation policy;
- PTY/process hosting remains orthogonal.

Adding Kitty Graphics in 1.8 does not move virtual-screen or graphics-scene ownership into `Icod.Terminal`. It implements the existing semantic raster output contract through another reviewed terminal protocol.

Adding unsolicited semantic events in 1.9 does not turn `Icod.Terminal` into a generic vendor-event bus. The public envelope remains typed, protocol-neutral, bounded, and delivered through the existing authoritative `ReadEventAsync(...)` path.

## 17. Direct consumers and Icod.DCurses

Direct consumers should use `TerminalSession` when they need live terminal/session mechanics without a curses virtual-screen model.

Applications needing windows/cells/diff/refresh should normally use `Icod.DCurses` and allow that layer to own the supplied session according to its integration contract.

Do not create independent state-owning sessions over the same physical terminal merely to divide responsibilities.

## 18. Security compatibility

Security boundaries are compatibility commitments, not optional implementation details.

Stable 1.x does not use a minor/patch release to quietly introduce:

- generic raw OSC/CSI/DCS/APC/vendor dispatch as the ordinary API;
- hazardous host-affecting OSC 9 commands;
- generic raw OSC 633/777/1337/99 dispatch replacing reviewed semantic surfaces;
- a public arbitrary CSI/DCS/Sixel/APC/Kitty writer merely because internal grammars exist;
- a generic raw unsolicited-event stream or arbitrary vendor-event dictionary;
- terminal-brand-triggered activation presented as capability truth;
- a competing protocol-specific input reader;
- automatic clipboard reads;
- hidden shell/environment metadata capture;
- authentication claims for terminal-supplied notification interaction reports;
- automatic image-file decoding or network/process side effects in raster display;
- hidden file/temp-file/shared-memory graphics transport;
- silent compositing of unsupported fractional-alpha raster data;
- cancellation-driven truncation of already-committed graphics transfers;
- automatic retry/backend switch after partial committed graphics output.

New security-sensitive semantic features require explicit typed API, bounded validation, documentation, and tests.

## 19. Deprecation policy

When an existing 1.x API can be replaced compatibly, deprecation is preferred before removal.

Ordinary removal should identify a replacement, document migration, mark the old surface obsolete where practical, preserve it through a reasonable migration interval, and remove it only in a major release.

Exceptional removal without a normal deprecation period is reserved for cases such as active security vulnerability or an impossible-to-support contract and still requires explicit release documentation.

## 20. Compatibility evidence

A release is not considered compatible merely because unit tests pass.

The repository maintains layered evidence including:

- retained historical public API fingerprints plus the current 1.9 fingerprint;
- Windows/Linux/macOS runtime/source validation;
- exact multi-TFM API snapshot agreement;
- fresh NuGet-only consumers for newly added or compatibility-critical semantic APIs;
- generated XML documentation verification;
- retained historical package consumers/contracts;
- current `Icod.DCurses` package-boundary integration/ownership tests;
- repeated ownership/disposal hardening;
- exact protocol regression vectors;
- resource-bound tests;
- release/distribution validation on configured architectures.

Version 1.8.0 completed the A180–A189 APC/Kitty Graphics program and retained the 1.7 public API fingerprint. Version 1.8.1 is a non-feature-bearing maintenance release that retains the same public and behavioral compatibility contract while strengthening release-facing documentation and executable sample coverage.

Version 1.9.0 adds the reviewed semantic-event and interactive Kitty notification surface recorded by the 1.9 baseline. E190–E198 qualify one-reader routing, query precedence, malformed/oversized semantic recovery, bounded backpressure, lifecycle/cancellation behavior, fresh NuGet-only multi-TFM consumption, and downstream DCurses compatibility. E199 freezes the release-facing package/documentation state and final Staging checkpoint.

Exact release qualification evidence belongs to the relevant pull-request workflow, merged `main` workflow, release notes, and GitHub Release rather than being hard-coded into this permanent policy document.

## 21. Release rule

A green feature checkpoint is not publication authorization.

For every stable release:

1. one unchanged final pull-request head must pass the complete Staging qualification matrix;
2. only that qualified exact head may be considered ready for merge;
3. merge remains an explicit maintainer action;
4. the resulting `main` head must pass Release distribution validation;
5. `v<semver>` tagging/publication remains a separate explicit maintainer action and must use the curated `docs/releases/<version>.md` notes.

These gates may evolve operationally, but equivalent compatibility evidence must exist before historical checks are removed.

## 22. Version 1.9 semantic-event compatibility

Version 1.9 is an additive minor release, not a reinterpretation of the existing input contract.

The public additions are:

```text
TerminalEventKind.Semantic = 4
TerminalEvent.Semantic
TerminalSemanticEventKind
TerminalSemanticEvent
TerminalNotificationEventKind
TerminalNotificationEvent
KittyNotificationOptions.ReportActivation
KittyNotificationOptions.ReportClose
KittyNotificationOptions.Buttons
```

The first semantic notification observations are activation, one-based button activation, close, and close-tracking-unavailable. `CloseTrackingUnavailable` preserves uncertainty rather than pretending an unobservable future close has already occurred.

Semantic events share the same authoritative session input path as ordinary input and typed query responses. They do not establish a second reader or callback stream. Applications using exhaustive `TerminalEventKind` switches must account for the new additive value when adopting 1.9.

Interactive notification reporting is explicit opt-in and requires a caller-supplied notification identifier. Existing noninteractive Kitty notification calls preserve their established behavior when the new properties are left at defaults.

Notification report identifiers and button numbers are untrusted correlation data. The typed API validates grammar and bounds but does not authenticate the terminal, desktop notification service, or user interaction.
