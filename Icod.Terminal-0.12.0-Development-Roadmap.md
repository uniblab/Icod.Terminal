# Icod.Terminal 0.12.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.12.0`  
**Development version:** `0.12.0`  
**Predecessor:** `0.11.0` — OSC 22 terminal mouse-pointer shape control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** semantic OSC 133 shell-integration / semantic-prompt markers  
**Status:** T127 stable metadata/package/docs closure complete; exact stable-head validation required

---

## 1. Release objective

`Icod.Terminal 0.12.0` adds a portable typed OSC 133 semantic-prompt core without exposing arbitrary OSC construction, vendor-specific metadata, or retained shell-history state.

Portable semantics:

```text
OSC 133 ; A ST          prompt start
OSC 133 ; B ST          command-input start / prompt end
OSC 133 ; C ST          command-output start / command executed
OSC 133 ; D ST          aborted/cancelled command region
OSC 133 ; D ; n ST      completed command with byte exit status 0..255
```

Canonical outbound termination is ST (`ESC \\`).

---

## 2. Frozen public surface

```csharp
ValueTask BeginPromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishCommandAsync(
	byte exitStatus,
	CancellationToken cancellationToken = default
);

ValueTask AbortCommandAsync(
	CancellationToken cancellationToken = default
);
```

The operations are independently callable. There is no public marker enum, generic OSC 133 payload API, nullable completion status, scoped command-region lease, or synthetic A -> B -> C -> D state machine.

The stable public delta is frozen in `docs/Public-API-Baseline-0.12.md`.

---

## 3. Architectural rules

0.12 reuses the established terminal-control architecture:

- specialized byte-exact internal OSC writers;
- complete-frame construction before commit;
- caller cancellation before commit;
- committed frames written with `CancellationToken.None`;
- no implicit marker flush;
- session-owned output serialization;
- interactive-output requirement;
- truthful emission-oriented support semantics;
- no emulator/OS/`TERM` capability inference;
- no second output transport.

OSC 133 markers are transient annotations rather than library-owned terminal modes. Suspend, resume, and disposal therefore emit no automatic OSC 133 marker and no cleanup/recovery marker is fabricated after failure.

---

## 4. Tranche closure

### T120 — contract/reference freeze

**Status:** Complete.  
**Version:** `0.12.0-alpha.1`.

Frozen portable A/B/C/D semantics, ST termination, byte completion status, explicit abort, independent-call model, lifecycle/failure posture, extension exclusions, and no raw public OSC 133 API.

Record: `docs/T120-OSC-133-Semantic-Prompt-Contract-and-Reference-Freeze.md`.

### T121 — byte-exact writer

**Status:** Complete.  
**Version:** `0.12.0-alpha.2`.

Implemented canonical A/B/C/bare-D/`D;0..255` framing, minimal decimal status encoding, non-cancellable commit, no implicit flush, and byte-exact tests.

Record: `docs/T121-OSC-133-Byte-Exact-Writer.md`.

### T122 — semantic marker model

**Status:** Complete.  
**Version:** `0.12.0-alpha.3`.

Implemented internal typed marker vocabulary with explicit abort versus completion distinction and exhaustive mapping to T121.

Record: `docs/T122-OSC-133-Semantic-Marker-Model.md`.

### T123 — session integration/order semantics

**Status:** Complete.  
**Version:** `0.12.0-alpha.4`.

Integrated markers with the existing `TerminalSession` output gate without adding retained shell-history state. Proved deterministic ordering, cancellation while queued, interactive-output rejection, failure propagation, and later independent calls.

Record: `docs/T123-OSC-133-Session-Marker-Integration-and-Ordering-Semantics.md`.

### T124 — public API

**Status:** Complete.  
**Version:** `0.12.0-alpha.5`.

Exposed exactly the five frozen public operations.

Record: `docs/T124-Public-OSC-133-Semantic-Prompt-API.md`.

### T125 — lifecycle/failure/order hardening

**Status:** Complete.  
**Version:** `0.12.0-alpha.6`.

Proved queued cancellation, post-commit non-cancellability, no compensation after transport failure, no automatic lifecycle/disposal markers, idempotent disposal with respect to OSC 133, and no fabricated command history after lifecycle re-entry failure.

Record: `docs/T125-OSC-133-Lifecycle-Failure-and-Ordering-Hardening.md`.

### T126 — composition and downstream acceptance

**Status:** Complete.  
**Version:** `0.12.0-alpha.7`.

Proved composition with text, OSC 0/7/8/9;4/22/52, DECSCUSR, presentation, rich-input leases, synchronized output, and active terminal queries. Added real `Icod.DCurses` refresh acceptance on net8/net9/net10 and wired it into PR/distribution validation.

Corrected cumulative alpha.7 head `4a2bf1e966ac26eb9ed05c192db3d1ddbbad0055` passed PR workflow #509.

Record: `docs/T126-OSC-133-Composition-and-DCurses-Acceptance.md`.

### T127 — public API, docs, sample, package, stable closure

**Status:** Stable closure implemented; exact stable-head validation required.  
**Version:** `0.12.0`.

Delivered:

- frozen `docs/Public-API-Baseline-0.12.md`;
- root README stable 0.12 documentation;
- solution-owned `Icod.Terminal.SemanticPrompt.Sample`;
- `samples/README.md` semantic-prompt documentation;
- final 0.12 package release notes/tags;
- fresh NuGet-only `package-semantic-prompt-smoke` consumer;
- `VerifySemanticPromptPackage.ps1` XML/public-package gate on net8/net9/net10;
- retained 0.8/0.9/0.10/0.11 package-contract gates;
- retained DCurses synchronized-output/progress/pointer-shape acceptance;
- retained T126 DCurses semantic-prompt acceptance;
- PR/distribution/tagged-release wiring for the new 0.12 gates;
- stable `VersionSuffix` empty metadata;
- T127 stable closure record.

Record: `docs/T127-0.12.0-Public-API-Package-and-Stable-Closure.md`.

---

## 5. Explicit non-goals

0.12 does not add:

- public arbitrary OSC construction;
- public raw OSC 133 strings/marker letters;
- arbitrary key/value OSC 133 metadata;
- automatic shell detection or shell-script installation;
- automatic PS1/PROMPT_COMMAND/startup-file modification;
- VS Code OSC 633 as though it were OSC 133;
- iTerm2 OSC 1337 as though it were OSC 133;
- Kitty-only metadata promoted into the portable core;
- terminal-emulator detection as a capability oracle;
- shell command parsing/execution;
- retained scrollback or shell-history database.

---

## 6. Stable candidate gate — current

Current metadata:

```text
VersionPrefix:   0.12.0
VersionSuffix:   <empty>
Version:         0.12.0
PackageVersion:  0.12.0
AssemblyVersion: 0.12.0.0
```

The exact stable PR head must pass:

- Windows/Linux/macOS source restore/build/test;
- solution-owned 0.12 semantic-prompt sample build;
- all four downstream DCurses acceptance gates;
- exact Staging package verification;
- retained 0.8/0.9/0.10/0.11 package/XML gates;
- new 0.12 package/XML + fresh-consumer gate.

No feature/public-API/protocol expansion belongs between that green stable head and merge.

---

## 7. Post-merge release gate

After merge:

1. require exact `main` Release/distribution validation green;
2. require all four downstream DCurses gates and all historical/new package gates green;
3. only then create `v0.12.0`;
4. the tagged workflow rebuilds/retests, reruns all downstream and package gates, selects the exact tagged package, then publishes to NuGet.org and GitHub Packages and creates the GitHub Release.
