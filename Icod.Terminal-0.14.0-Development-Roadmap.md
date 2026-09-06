# Icod.Terminal 0.14.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.14.0`  
**Development version:** `0.14.0-alpha.5`  
**Predecessor:** `0.13.0` — observable terminal palette and dynamic-color control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** lifecycle-safe color ownership/restoration and bounded terminal protocol closure  
**Status:** T140–T143 complete/green; T144 hardening implemented with exact-head validation pending

---

## 1. Position on the road to 1.0

```text
0.14.0       lifecycle-safe color ownership/restoration and other terminal protocol work
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support dates alone do not remove net8/net9 support. Reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance issue.

0.14 SHALL NOT pre-implement OSC 133 extended metadata, OSC 9 safe extensions, modern negotiated keyboard protocols, or the broad 0.18 hardening campaign.

---

## 2. Frozen lifecycle-safe color contract

T140 freezes the following release invariants.

### Query before first mutation

The first scoped owner for one palette index or dynamic-color identity SHALL observe a real terminal baseline before mutation. Timeout, cancellation, malformed correlated response, or other observation failure leaves no owner and performs no color mutation.

### Exact replay, never reset masquerading as restoration

Final scoped restoration explicitly replays the observed `TerminalColor` using the corresponding set form.

```text
palette: OSC 4 ; index ; observed-color ST
dynamic: OSC Ps ; observed-color ST
```

OSC 104 and OSC 110–119 remain terminal-policy reset APIs and SHALL NOT implement scoped restoration.

### Identity-aware ownership

For each palette index or dynamic-color identity:

- first owner captures the external baseline;
- later owners supersede the effective requested value;
- disposing a non-controlling owner does not alter physical state;
- disposing the controlling owner reapplies the next active owner;
- disposing the final owner restores the external baseline;
- out-of-order disposal is supported.

Different identities remain logically independent.

### Failure ownership

If restoration fails, logical ownership remains retained when retry is meaningful. Failed cleanup is never silently converted into success or reset-to-policy.

### Invalidation

`TerminalSession.InvalidateState()` marks physical color state uncertain without erasing logical owners or the current lifecycle-epoch external baseline. The next safe ownership transition re-establishes the effective owned state before relying on physical state.

### Suspend/resume lifecycle epochs

Before managed suspend, scoped color managers restore their external baselines.

After resume, pre-suspend observations are no longer authoritative. Retained owners establish fresh external baselines through the internal T141 observation window before owned values are reapplied.

### Session disposal

Session disposal is the final cleanup owner. Active leases need not be disposed first. Successful session cleanup restores the latest truthful external baselines and makes later lease disposal a no-op.

---

## 3. Lifecycle/query architecture

T141 implements a distinct internal post-resume observation phase without widening the public query contract.

Public terminal queries remain unavailable throughout lifecycle re-entry and ordinary `ITerminalSessionLifecycleParticipant` resume callbacks.

Observation-dependent session-owned managers use internal `ITerminalObservedLifecycleParticipant` and `ExecuteLifecycleObservationQueryAsync(...)` while the public query gate remains suspended.

Effective resume order:

```text
1. reacquire native/output host state and input mode
2. resume non-observational Terminal-owned presentation/input-protocol state
3. internal observation window for session-owned observation-dependent managers
4. resume ordinary lifecycle participants
5. resume public query transactions
6. mark state valid and publish Resumed
```

The observation window reuses:

- the single underlying input reader;
- the active-query transaction manager;
- expectation-driven response routing;
- ambiguity-sensitive query serialization;
- bounded late-response ownership;
- existing timeout/cancellation distinction;
- preservation of unrelated application input.

Records:

- `docs/T140-Lifecycle-Safe-Color-Ownership-and-Resume-Observation-Contract.md`
- `docs/T141-Internal-Post-Resume-Observation-Query-Foundation.md`

---

## 4. Public ownership API

### Indexed palette

```csharp
ValueTask<TerminalPaletteColorLease> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

`TerminalPaletteColorLease` implements `IAsyncDisposable` and exposes `Index` and requested `Color`.

### Dynamic colors

```csharp
ValueTask<TerminalDynamicColorLease> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

`TerminalDynamicColorLease` implements `IAsyncDisposable` and exposes `Kind` and requested `Color`.

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

Acquisition failure semantics remain ordinary active-query semantics:

- timeout -> `TimeoutException`;
- caller cancellation -> cancellation;
- malformed correlated reply -> `FormatException`;
- transport/session failure remains distinct;
- no response is not translated into permanent unsupported state.

---

## 5. Completed tranches

### T140 — lifecycle-safe color ownership contract and lifecycle-order freeze

**Version:** `0.14.0-alpha.1`  
**Status:** Complete and green.  
**Validation:** workflow #611.

Record: `docs/T140-Lifecycle-Safe-Color-Ownership-and-Resume-Observation-Contract.md`.

### T141 — reusable observed-state ownership foundation

**Version:** `0.14.0-alpha.2`  
**Status:** Complete and green.  
**Validation:** workflow #618.

Delivered the internal post-resume observation query window while retaining public query suspension and the one-reader architecture.

Record: `docs/T141-Internal-Post-Resume-Observation-Query-Foundation.md`.

### T142 — indexed palette scoped ownership

**Version:** `0.14.0-alpha.3`  
**Status:** Complete and green.  
**Validation:** workflow #628.

Delivered:

- `TerminalPaletteColorLease`;
- first-owner OSC 4 observation before mutation;
- exact 16-bit baseline replay;
- same-index nesting and out-of-order release;
- independent palette-index ownership;
- no OSC 104 restoration;
- unscoped palette mutation/reset exclusion while scoped palette ownership exists;
- invalidation recovery;
- suspend restoration and post-resume fresh observation;
- transactional multi-index resume rollback;
- exact session-disposal cleanup.

Record: `docs/T142-Lifecycle-Safe-Indexed-Palette-Ownership.md`.

### T143 — dynamic-color scoped ownership

**Version:** `0.14.0-alpha.4`  
**Status:** Complete and green.  
**Validation:** workflow #635.

Delivered the same truthful ownership model across all seven selected non-Tektronix dynamic-color identities, including common/extended lifecycle re-observation coverage and transactional multi-identity resume rollback.

Record: `docs/T143-Lifecycle-Safe-Dynamic-Color-Ownership.md`.

### T144 — lifecycle failure and concurrency hardening

**Version:** `0.14.0-alpha.5`  
**Status:** Implemented; exact-head validation pending.

Hardening coverage includes:

- cancellation before baseline-query emission;
- cancellation after query emission and bounded late-response ownership;
- safe later acquisition after a cancelled query receives a late correlated reply;
- suspend interrupting an outstanding first-owner baseline query without deadlock or phantom ownership;
- failed palette restoration retaining ownership for disposal retry;
- failed dynamic-color restoration retaining ownership for disposal retry;
- concurrent palette/dynamic acquisition through the shared ambiguity-sensitive query router;
- concurrent release through the shared output serialization domain;
- session disposal with palette and dynamic leases still outstanding;
- late lease disposal as a no-op after successful session cleanup.

No new public API or protocol family is introduced by T144.

Record: `docs/T144-Lifecycle-Failure-and-Color-Ownership-Concurrency-Hardening.md`.

---

## 6. Remaining tranche sequence

### T145 — bounded terminal protocol closure

**Expected version:** `0.14.0-alpha.6`.

Audit concrete protocol gaps exposed by T140–T144. Add protocol work only when all of the following hold:

1. it strengthens an existing 0.14 ownership/lifecycle contract;
2. it is not assigned to 0.15, 0.16, or 0.17;
3. it has a semantic typed API or remains internal plumbing;
4. it can be bounded and deterministically tested;
5. it does not create a generic raw-protocol escape hatch.

If no qualifying gap exists, T145 SHALL explicitly document that result and add no feature merely to consume the tranche number.

### T146 — composition and downstream acceptance

**Expected version:** `0.14.0-alpha.7`.

Prove lifecycle-safe color ownership composes with:

- ordinary application output;
- presentation leases;
- focus/paste/mouse input-protocol leases;
- cursor style;
- synchronized output;
- progress;
- pointer shape;
- OSC 7 location;
- OSC 8 hyperlinks;
- OSC 52 clipboard;
- OSC 133 core semantic markers;
- active terminal queries.

Extend downstream `Icod.DCurses` acceptance so a real consumer can acquire/release lifecycle-safe color ownership without raw OSC parsing or private lifecycle handling.

### T147 — public API/package/stable closure

**Expected stable version:** `0.14.0`.

Deliver:

- `docs/Public-API-Baseline-0.14.md`;
- README and focused sample updates;
- XML documentation assertions for the complete 0.14 public delta;
- fresh NuGet-only consumer on net8.0/net9.0/net10.0;
- retained 0.8–0.13 package-contract gates;
- retained downstream acceptance gates;
- new lifecycle-safe color ownership package/downstream gates;
- stable release notes/tags;
- exact PR/main/tag release validation.

---

## 7. Testing requirements

0.14 SHALL retain or add coverage for:

- query-before-mutate;
- no mutation after failed baseline observation;
- exact 16-bit baseline replay;
- no reset-based restoration;
- nesting and out-of-order release;
- independent color identities;
- invalidation and physical-state uncertainty;
- suspend restore -> external change -> resume re-observe -> owned-state reapply;
- partial resume rollback to refreshed baselines;
- cancellation before and after query emission;
- late correlated responses;
- restoration failure and retry ownership;
- suspend while baseline query is outstanding;
- concurrent palette/dynamic acquisition and release;
- session disposal with outstanding owners;
- public query rejection during internal observation and ordinary lifecycle participant resume;
- internal observation query admission;
- unrelated application input preservation;
- Windows, Linux, and macOS CI;
- net8.0, net9.0, and net10.0 package-only consumers;
- downstream `Icod.DCurses` acceptance.

---

## 8. Explicit non-goals

0.14 SHALL NOT add:

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

## 9. Current development state

```text
VersionPrefix:    0.14.0
VersionSuffix:    alpha.5
Version:          0.14.0-alpha.5
PackageVersion:   0.14.0-alpha.5
AssemblyVersion:  0.14.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T144 validation:** T145 — evidence-based bounded terminal protocol closure audit.
