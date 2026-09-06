# Icod.Terminal 0.14.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.14.0`  
**Development version:** `0.14.0`  
**Predecessor:** `0.13.0` — observable terminal palette and dynamic-color control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** lifecycle-safe color ownership/restoration and bounded terminal protocol closure  
**Status:** T140–T146 complete/green; T147 stable closure implemented, exact-head validation pending

---

## 1. Position on the road to 1.0

```text
0.14.0       lifecycle-safe color ownership/restoration
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support dates alone do not remove net8/net9 support. Reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance issue.

0.14 deliberately does not pre-implement the work assigned to 0.15–0.18.

---

## 2. Stable lifecycle-safe color contract

### Query before first mutation

The first scoped owner for one palette index or dynamic-color identity observes the real terminal baseline before mutation. Timeout, cancellation, malformed correlated response, or another observation failure leaves no owner and performs no color mutation.

### Exact replay, never reset masquerading as restoration

Final scoped restoration explicitly replays the observed `TerminalColor` using the corresponding set form.

```text
palette: OSC 4 ; index ; observed-color ST
dynamic: OSC Ps ; observed-color ST
```

OSC 104 and OSC 110–119 remain terminal-policy reset APIs and are never used to implement scoped restoration.

### Identity-aware ownership

For each palette index or dynamic-color identity:

- the first owner captures the external baseline;
- later owners supersede the effective requested value;
- disposing a non-controlling owner does not alter physical state;
- disposing the controlling owner reapplies the next active owner;
- disposing the final owner restores the external baseline;
- out-of-order disposal is supported;
- different identities remain logically independent.

### Failure ownership

If restoration fails, logical ownership remains retained when retry is meaningful. Failed cleanup is never silently converted into success or reset-to-policy.

### Invalidation

`TerminalSession.InvalidateState()` marks physical color state uncertain without erasing logical owners or the current lifecycle-epoch baseline. The next safe ownership transition re-establishes effective owned state before relying on physical state.

### Suspend/resume lifecycle epochs

Before managed suspend, scoped color managers restore their external baselines.

After resume, pre-suspend observations are no longer authoritative. Retained owners establish fresh external baselines through the internal T141 observation window before owned values are reapplied.

### Session disposal

Session disposal is the final cleanup owner. Active leases need not be disposed first. Successful session cleanup restores the latest truthful external baselines and makes later lease disposal a no-op.

---

## 3. Stable public ownership API

### Indexed palette

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

`TerminalPaletteColorLease : IAsyncDisposable` exposes `Index` and requested `Color`.

### Dynamic colors

```csharp
ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

`TerminalDynamicColorLease : IAsyncDisposable` exposes `Kind` and requested `Color`.

Supported dynamic identities remain:

```text
DefaultForeground      OSC 10
DefaultBackground      OSC 11
TextCursor             OSC 12
MouseForeground        OSC 13
MouseBackground        OSC 14
HighlightBackground    OSC 17
HighlightForeground    OSC 19
```

OSC 10–12 remain the common/core interoperability tier. OSC 13/14/17/19 remain the extended xterm tier. No terminal-brand inference is used.

---

## 4. Lifecycle/query architecture

T141 provides a distinct internal post-resume observation phase without widening the public query contract.

Effective resume order:

```text
1. reacquire native/output host state and input mode
2. resume non-observational Terminal-owned presentation/input-protocol state
3. internal observation window for session-owned observation-dependent managers
4. resume ordinary lifecycle participants
5. resume public query transactions
6. mark state valid and publish Resumed
```

The observation window reuses the single input reader, active-query transaction manager, expectation-driven response routing, ambiguity serialization, bounded late-response ownership, and existing timeout/cancellation semantics.

---

## 5. Tranche record

### T140 — lifecycle-safe color ownership contract and lifecycle-order freeze

**Version:** `0.14.0-alpha.1`  
**Status:** Complete and green  
**Validation:** workflow #611

Record: `docs/T140-Lifecycle-Safe-Color-Ownership-and-Resume-Observation-Contract.md`.

### T141 — reusable observed-state ownership foundation

**Version:** `0.14.0-alpha.2`  
**Status:** Complete and green  
**Validation:** workflow #618

Record: `docs/T141-Internal-Post-Resume-Observation-Query-Foundation.md`.

### T142 — indexed-palette scoped ownership

**Version:** `0.14.0-alpha.3`  
**Status:** Complete and green  
**Validation:** workflow #628

Record: `docs/T142-Lifecycle-Safe-Indexed-Palette-Ownership.md`.

### T143 — dynamic-color scoped ownership

**Version:** `0.14.0-alpha.4`  
**Status:** Complete and green  
**Validation:** workflow #635

Record: `docs/T143-Lifecycle-Safe-Dynamic-Color-Ownership.md`.

### T144 — lifecycle failure and concurrency hardening

**Version:** `0.14.0-alpha.5`  
**Status:** Complete and green  
**Validation:** workflow #641

Coverage includes cancellation before/after query emission, bounded late-response ownership, suspend interruption of outstanding acquisition, cleanup retry, cross-manager query/output serialization, and session disposal with outstanding leases.

Record: `docs/T144-Lifecycle-Failure-and-Color-Ownership-Concurrency-Hardening.md`.

### T145 — bounded terminal protocol closure

**Version:** `0.14.0-alpha.6`  
**Status:** Complete

The audit found no concrete protocol gap that justified expanding the 0.14 surface. No protocol was added merely to fill the tranche.

Record: `docs/T145-Bounded-Terminal-Protocol-Closure-Audit.md`.

### T146 — composition and downstream acceptance

**Version:** `0.14.0-alpha.7`  
**Status:** Complete and green  
**Validation:** workflow #649

The real `Icod.DCurses 0.1.0` acceptance consumer proves scoped palette/dynamic ownership, normal DCurses RGB rendering, ownership transfer of a supplied `TerminalSession`, exact baseline restoration on downstream disposal, and silent late lease disposal.

Record: `docs/T146-Composition-and-DCurses-Scoped-Color-Acceptance.md`.

### T147 — public API/package/stable closure

**Version:** `0.14.0`  
**Status:** Stable closure implemented; exact-head validation pending

Delivered:

- `docs/Public-API-Baseline-0.14.md`;
- stable `0.14.0` repository/package version metadata;
- 0.14 package release notes and tags;
- root README rewritten for lifecycle-safe color ownership;
- focused color sample updated to use scoped ownership rather than reset-based pseudo-restoration;
- fresh NuGet-only `tools/package-color-ownership-smoke` consumer on net8/net9/net10;
- `packaging/VerifyColorOwnershipPackage.ps1` XML/public-surface gate;
- PR and tagged-release workflow integration for the new 0.14 package contract;
- retained 0.8–0.13 historical package-contract gates;
- retained downstream acceptance gates.

Record: `docs/T147-0.14.0-Public-API-Package-and-Stable-Closure.md`.

---

## 6. Testing and release gates

The stable PR head must prove:

- Windows, Linux, and macOS build/test success;
- net8.0, net9.0, and net10.0 test success;
- all retained downstream `Icod.DCurses` acceptance gates;
- exact Staging package selection/metadata;
- retained 0.8–0.13 package contracts;
- new 0.14 lifecycle-safe color ownership XML and fresh-package consumer contract.

After merge, the exact `main` commit must pass Release validation before tag `v0.14.0` is created. The tagged workflow must then repeat release package validation before NuGet.org/GitHub Packages publication.

---

## 7. Explicit non-goals

0.14 does not add:

- OSC 133 extended metadata;
- OSC 9 notification/CWD extensions;
- modern keyboard negotiation;
- generic public OSC/CSI/DCS construction;
- terminal emulation;
- PTY/ConPTY process hosting;
- graphics protocols;
- Tektronix color controls;
- reset-based fake restoration;
- terminal-brand heuristics as a support oracle;
- removal of net8.0 or net9.0 merely because of vendor lifecycle status.

---

## 8. Current stable-candidate state

```text
VersionPrefix:    0.14.0
VersionSuffix:
Version:          0.14.0
PackageVersion:   0.14.0
AssemblyVersion:  0.14.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next gate:** exact stable PR-head validation. After that, merge to `main`, validate the exact main commit under Release configuration, and only then create `v0.14.0`.
