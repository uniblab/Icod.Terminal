# Icod.Terminal 0.11.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.11.0`  
**Development version:** `0.11.0-alpha.7`  
**Predecessor:** `0.10.0` — OSC 9;4 terminal progress ownership  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** semantic OSC 22 terminal mouse-pointer shape control and ownership  
**Status:** T110–T116 complete; T117 stable closure in progress

---

## 1. Release objective

`Icod.Terminal 0.11.0` adds a semantic OSC 22 pointer-shape subsystem without exposing arbitrary OSC construction or terminal-specific raw pointer-name injection.

The release provides:

- 30 CSS-compatible semantic pointer shapes;
- byte-exact OSC 22 set/reset framing;
- explicit semantic set/reset operations;
- identity-aware scoped pointer ownership;
- deterministic restoration among Icod-owned nested shapes;
- out-of-order-safe disposal;
- explicit bounded Kitty-compatible pointer queries;
- managed suspend/resume participation;
- invalidation and failure recovery;
- authoritative session-disposal reset;
- composition with existing terminal operations and synchronized output;
- real downstream `Icod.DCurses` pointer-shape acceptance.

OSC 22 mouse-pointer shape remains distinct from DECSCUSR text-cursor style and cursor visibility.

---

## 2. Architectural rules

0.11 preserves the established terminal-control architecture:

- semantic public APIs rather than arbitrary OSC construction;
- specialized internal OSC writers;
- complete-frame construction before commit;
- caller cancellation before commit;
- committed control frames written non-cancellably;
- session-owned output serialization;
- interactive-output requirement;
- truthful optimistic support semantics;
- lifecycle participants for scoped terminal state;
- retryable cleanup debt;
- authoritative session-disposal cleanup;
- explicit queries through the existing active-query transaction manager;
- no second reader or output transport.

No `Icod.TermInfo` database change is required for OSC 22 pointer shape.

---

## 3. Tranche record

| Tranche | Version | Status | Outcome |
| --- | --- | --- | --- |
| T110 | `0.11.0-alpha.1` | Complete | OSC 22 reference, vocabulary, ownership, lifecycle, and query contract frozen. |
| T111 | `0.11.0-alpha.2` | Complete | Byte-exact OSC 22 set/reset writer with canonical ST termination. |
| T112 | `0.11.0-alpha.3` | Complete | Public 30-value semantic pointer model and strict canonical codec. |
| T113 | `0.11.0-alpha.4` | Complete | Session-owned identity-aware pointer manager and nested restoration. |
| T114 | `0.11.0-alpha.5` | Complete | Public setter/reset/lease APIs and explicit Kitty-compatible queries. |
| T115 | `0.11.0-alpha.6` | Complete | Lifecycle, invalidation, failure recovery, retryable cleanup, and disposal hardening. |
| T116 | `0.11.0-alpha.7` | Complete | Composition plus real `Icod.DCurses` pointer-shape refresh acceptance. |
| T117 | `0.11.0` | In progress | Public API/package/docs/sample/stable closure. |

Validation evidence:

- T111 workflow #444 green;
- T112 workflow #449 green;
- T113 workflow #456 green;
- T114 workflow #466 green;
- T115 workflow #467 green;
- T116 workflow #471 green.

---

## 4. T110 — contract/reference freeze

Frozen in `docs/T110-OSC-22-Pointer-Shape-Contract-and-Reference-Freeze.md`:

- canonical `ESC ] 22 ; shape ESC \\` set framing;
- canonical empty-payload terminal-policy reset;
- 30 CSS-compatible semantic pointer names;
- strict distinction between CSS `default` and terminal-policy reset;
- truthful optimistic support semantics;
- semantic set/reset APIs;
- identity-aware nested ownership independent of Kitty terminal-side stacks;
- suspend/resume/invalidation/failure behavior;
- explicit bounded Kitty-compatible query disposition;
- no raw pointer strings or automatic emulator inference.

---

## 5. T111 — byte-exact OSC 22 writer

Implemented:

- specialized internal OSC 22 encoder/writer;
- canonical ST termination;
- strict canonical-name validation;
- terminal-policy reset through empty payload;
- one complete non-cancellable committed write;
- no implicit flush;
- byte-exact and cancellation tests.

Record: `docs/T111-OSC-22-Byte-Exact-Writer.md`.

---

## 6. T112 — semantic pointer-shape model

Implemented:

- public `TerminalPointerShape` with exactly 30 semantic values;
- exact canonical wire-name mapping;
- strict reverse parsing for explicit queries;
- rejection of casing variants, X11 aliases, lists, stack/query syntax, and unknown names;
- explicit distinction between semantic `Default` and terminal-policy reset.

Record: `docs/T112-Semantic-Pointer-Shape-Model.md`.

---

## 7. T113 — session pointer-shape manager and nesting

Implemented:

- newest active owner controls physical pointer shape;
- nested controlling release restores newest remaining Icod-owned shape;
- out-of-order non-controlling release is silent;
- final release resets to terminal policy;
- no lifetime output-gate ownership;
- suspend/resume, invalidation, and session-disposal integration;
- redirected-output rejection;
- cleanup debt and physical-state uncertainty tracking.

Record: `docs/T113-Session-Pointer-Shape-Manager-and-Nesting.md`.

---

## 8. T114 — public pointer-shape API and queries

Public surface:

```csharp
TerminalPointerShape
TerminalPointerShapeLease
TerminalPointerShapeObservation

TerminalSession.SetPointerShapeAsync(...)
TerminalSession.ResetPointerShapeAsync(...)
TerminalSession.AcquirePointerShapeAsync(...)
TerminalSession.QueryCurrentPointerShapeAsync(...)
TerminalSession.QueryDefaultPointerShapeAsync(...)
TerminalSession.QueryGrabbedPointerShapeAsync(...)
TerminalSession.QueryPointerShapeSupportAsync(...)
```

Queries reuse the existing active-query transaction manager and accept seven-bit ST, BEL, and C1 OSC response framing.

A current-state reply of `0` is represented as no application shape and remains distinct from CSS `Default`.

Record: `docs/T114-Public-Pointer-Shape-Lease-Setter-and-Query-API.md`.

---

## 9. T115 — lifecycle, invalidation, failure, disposal

Implemented and proved:

- failed outermost acquisition resets to terminal policy before rethrow;
- failed nested acquisition restores the prior Icod-owned shape;
- acquisition/recovery double failures aggregate;
- invalidation is recoverable by the next semantic transition;
- failed controlling release retains the lease for retry;
- failed manager/session cleanup remains retryable;
- failed explicit mutation performs best-effort reset;
- re-entry/cleanup double failures aggregate;
- late lease disposal after successful session cleanup emits nothing.

Record: `docs/T115-Pointer-Shape-Lifecycle-Invalidation-Failure-and-Disposal-Hardening.md`.

---

## 10. T116 — composition and downstream acceptance

Implemented and proved composition with:

- ordinary text;
- OSC 0 title;
- OSC 7 location;
- OSC 8 hyperlinks;
- OSC 9;4 progress;
- OSC 52 clipboard;
- DECSCUSR text-cursor style;
- presentation state;
- reversible input-protocol leases;
- synchronized output;
- active terminal queries.

Real downstream acceptance uses published `Icod.DCurses 0.1.0` with current Terminal source and proves nested pointer-shape transitions occur around actual `CursesSession.RefreshAsync()` payloads on net8.0/net9.0/net10.0.

The pointer-shape DCurses verifier is required by PR, distribution, and tagged-release validation alongside the retained synchronized-output and progress gates.

Record: `docs/T116-Pointer-Shape-Composition-and-DCurses-Acceptance.md`.

---

## 11. T117 — public API, docs, sample, package, stable closure

Prepared on the alpha.7 line:

- frozen `docs/Public-API-Baseline-0.11.md`;
- solution-owned `Icod.Terminal.PointerShape.Sample`;
- root README 0.11 release-candidate documentation;
- `samples/README.md` pointer-shape documentation;
- 0.11 package release notes and tags;
- publication-grade pointer query XML documentation;
- fresh NuGet-only pointer-shape consumer on net8.0/net9.0/net10.0;
- XML-documentation assertions for the complete 0.11 public delta including all 30 enum members;
- retained 0.8, 0.9, and 0.10 package-contract gates;
- new 0.11 pointer-shape package gate in PR/distribution/release validation.

Stable closure remains gated on:

1. cumulative exact alpha.7 head green on Windows/Linux/macOS;
2. all three DCurses acceptance gates green;
3. fresh 0.11 package-only consumer/XML-doc gate green;
4. empty `VersionSuffix` and stable README/status wording;
5. exact stable PR head green;
6. merge to `main`;
7. Release distribution validation green on exact `main`;
8. only then tag `v0.11.0`.

Record: `docs/T117-0.11.0-Public-API-Package-and-Stable-Closure.md`.

---

## 12. Explicit non-goals

0.11 does not add:

- public arbitrary OSC construction;
- public arbitrary OSC 22 strings;
- arbitrary X11 pointer-name injection;
- Kitty pointer-stack push/pop APIs;
- automatic terminal-emulator detection;
- automatic support probing during ordinary pointer mutation/acquisition;
- platform GUI pointer APIs;
- retained application-side pointer rendering.

---

## 13. Current development state

```text
VersionPrefix:   0.11.0
VersionSuffix:   alpha.7
Version:         0.11.0-alpha.7
PackageVersion:  0.11.0-alpha.7
AssemblyVersion: 0.11.0.0
```

**T117 stable closure is the current tranche.**
