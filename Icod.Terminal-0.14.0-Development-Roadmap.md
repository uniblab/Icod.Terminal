# Icod.Terminal 0.14.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.14.0`  
**Development version:** `0.14.0-alpha.1`  
**Predecessor:** `0.13.0` — observable terminal palette and dynamic-color control  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** lifecycle-safe color ownership/restoration and terminal protocol completion work  
**Status:** roadmap established; T140 contract/lifecycle redesign next

---

## 1. Position on the road to 1.0

The planned release sequence is:

```text
0.14.0       lifecycle-safe color ownership/restoration and other terminal protocol work
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain supported. The project will not remove `net8.0` or `net9.0` merely because their vendor support window ends; those TFMs remain part of the Icod.Terminal compatibility contract unless a concrete security alert or security-maintenance constraint requires reconsideration.

0.14 SHALL NOT pre-implement the work assigned to 0.15–0.18 merely to accelerate 1.0. In particular, OSC 133 extended metadata, OSC 9 safe extensions, modern negotiated keyboard protocols, and the final broad hardening campaign retain their own release lines.

---

## 2. Primary 0.14 objective — truthful color ownership

0.13 deliberately shipped palette and dynamic-color mutation as unscoped operations. T136 established why a lifecycle-safe lease could not be added honestly without changing lifecycle/query ordering:

- exact restoration requires an observed baseline;
- reset-to-terminal-policy is not restoration;
- a pre-suspend observation may no longer be authoritative after resume;
- a truthful owner must be able to re-observe after resume before reapplying owned state;
- the 0.13 lifecycle pipeline resumes registered participants before active queries are re-enabled.

0.14 SHALL solve that architectural problem rather than weaken the restoration claim.

The release SHALL support lifecycle-safe ownership/restoration for:

1. indexed palette entries managed through OSC 4;
2. selected dynamic colors managed through OSC 10–14, 17, and 19.

A successful scoped acquisition SHALL mean that `Icod.Terminal` has a real observed baseline and owns enough state to restore it exactly under the documented lifecycle contract.

---

## 3. Restoration invariants

The following are release-level invariants.

### 3.1 Query before first mutation

A color lease SHALL NOT mutate terminal color state until the relevant baseline has been observed successfully.

If observation times out, is cancelled, is malformed, or otherwise fails, acquisition fails without retaining a mutation or restoration owner.

### 3.2 Exact replay, never reset masquerading as restoration

Lease restoration SHALL replay the observed `TerminalColor` explicitly:

```text
palette: OSC 4 ; index ; observed-color ST
dynamic: OSC Ps ; observed-color ST
```

OSC 104 and OSC 110–119 remain terminal-policy reset APIs and SHALL NOT be used to implement exact lease restoration.

### 3.3 Nested ownership

Nested ownership SHALL be deterministic.

The roadmap preference is identity-aware ordered ownership:

- first owner establishes the external baseline;
- later owners may supersede the effective requested color;
- releasing the effective owner reapplies the next active owner's requested color;
- releasing the final owner restores the external baseline;
- out-of-order disposal cannot restore over a still-active owner.

T140/T141 SHALL freeze whether this is implemented by one shared color-state manager or coordinated palette/dynamic managers.

### 3.4 Failure ownership

If a release/restoration write fails and rollback cannot prove restoration, the lease SHALL remain logically responsible for cleanup when retry is meaningful. A failed cleanup SHALL NOT be silently converted into success or into terminal-policy reset.

### 3.5 Invalidation

`TerminalSession.InvalidateState()` SHALL mark physical color state untrusted without erasing the logical ownership stack or the external baseline already established for the current lifecycle epoch.

The next safe transition SHALL re-establish effective owned state from logical ownership rather than assuming the terminal still reflects the last successful write.

### 3.6 Suspend/resume truthfulness

Before managed suspend, active scoped color ownership SHALL restore the externally observed baseline where the lifecycle contract requires giving the terminal back to the host/shell.

After resume, the library SHALL NOT blindly reuse a pre-suspend baseline as though no external actor could have changed terminal colors while the process was stopped.

The lifecycle/query architecture SHALL provide a defined post-resume observation phase in which scoped color managers can establish a new external baseline before reapplying active owned colors.

This redesign must preserve the existing one-reader and active-query ownership invariants.

### 3.7 Disposal

Session disposal remains the final cleanup owner. Active color leases need not be disposed before the session itself.

Disposal SHALL make a best-effort exact restoration from the latest truthful baseline available for the current lifecycle epoch and combine cleanup failures with the existing session restoration failure path.

Late disposal of a lease after successful session cleanup SHALL be a no-op.

---

## 4. Lifecycle/query redesign boundary

0.14 may change internal lifecycle ordering, but SHALL NOT weaken the public query contract established in 0.3.

Required properties remain:

- exactly one underlying input reader;
- expectation-driven response routing;
- ambiguity-sensitive query serialization;
- bounded late-response ownership;
- caller cancellation distinct from timeout;
- unrelated application input preserved;
- no second color response reader;
- no automatic general terminal probing during ordinary session open.

The preferred redesign is a distinct post-resume phase:

```text
reacquire native/output host state
re-enable the active-query path
allow lifecycle participants that require observation to refresh baselines
reapply retained logical terminal ownership
publish Resumed
```

T140 SHALL audit all existing lifecycle participants before freezing this ordering. Presentation, input-protocol, cursor-style, synchronized-output, pointer-shape, progress, and other managers must not regress merely to make color leases possible.

---

## 5. Public API direction

Exact names remain subject to T140/T141 review, but 0.14 is expected to add scoped acquisition operations analogous in spirit to existing reversible terminal ownership APIs.

Candidate semantic surface:

```csharp
ValueTask<TerminalControlResult<TerminalPaletteColorLease>> AcquirePaletteColorAsync(
	byte index,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);

ValueTask<TerminalControlResult<TerminalDynamicColorLease>> AcquireDynamicColorAsync(
	TerminalDynamicColor kind,
	TerminalColor color,
	TimeSpan queryTimeout,
	CancellationToken cancellationToken = default
);
```

This shape is provisional. T140 SHALL decide:

- whether `TerminalControlResult<T>` is the correct unsupported/unavailable carrier when color-query support is discovered only by timeout;
- whether acquisition should surface query timeout directly rather than translating it;
- whether palette and dynamic ownership need separate public lease types;
- whether one generalized color-lease type would lose useful semantic identity;
- whether lease objects expose their requested color/index/kind;
- disposal/retry semantics after failed restoration.

No public API is frozen until that review is complete.

---

## 6. Other terminal protocol work in 0.14

0.14 may close **small, bounded protocol gaps that materially improve the existing live-terminal foundation**, provided they do not belong to a later dedicated release.

Candidates SHALL be selected during T145 from evidence gathered while implementing lifecycle-safe ownership. Suitable examples include:

- protocol response-framing interoperability needed by color re-observation;
- lifecycle ordering hooks useful to multiple existing reversible managers;
- small missing standardized/private-mode query or restoration forms required to make existing ownership contracts more truthful;
- bounded parser compatibility corrections exposed by real terminal fixtures.

Explicitly reserved for later releases and therefore out of scope for 0.14:

- OSC 133 extended prompt/command metadata — 0.15;
- OSC 9 notification/CWD safe extensions — 0.16;
- CSI-u, Kitty keyboard protocol, `modifyOtherKeys`, key press/repeat/release contracts — 0.17;
- broad fuzz/stress/real-terminal compatibility campaign and ecosystem convergence — 0.18;
- 1.x API freeze and permanent documentation set — 1.0.0-rc1.

T145 SHALL require an explicit contract note before adding any extra protocol. “While we are here” is not sufficient justification.

---

## 7. Tranche sequence

### T140 — lifecycle-safe color ownership contract and lifecycle-order freeze

**Version:** `0.14.0-alpha.1`  
**Status:** Next.

Audit and freeze:

- T136 constraints;
- current lifecycle participant ordering;
- post-resume active-query availability;
- baseline epoch semantics;
- invalidation semantics;
- exact restoration versus reset;
- nested ownership ordering;
- cleanup/retry behavior;
- public acquisition/result/lease shape;
- interaction with existing presentation/input/cursor/synchronized-output/pointer managers.

No color lease implementation should precede this freeze.

### T141 — reusable observed-state ownership foundation

**Expected version:** `0.14.0-alpha.2`.

Implement the internal machinery required for lifecycle participants that must observe terminal state before mutation/re-entry:

- lifecycle post-resume observation phase;
- query availability during that phase;
- baseline lifecycle epochs;
- bounded failure propagation;
- deterministic participant ordering;
- tests proving existing lifecycle participants remain unchanged where observation is unnecessary.

Keep generic machinery internal unless concrete evidence justifies a public abstraction.

### T142 — indexed palette scoped ownership

**Expected version:** `0.14.0-alpha.3`.

Implement:

- query-before-mutate palette acquisition;
- exact external-baseline capture;
- nested same-index ownership;
- independent ownership of different palette indices;
- out-of-order disposal;
- explicit baseline replay on final release;
- failed acquisition with no mutation;
- failed release/retry semantics;
- invalidation re-establishment;
- suspend baseline restoration and post-resume re-observation/reapply;
- session-disposal cleanup.

### T143 — dynamic-color scoped ownership

**Expected version:** `0.14.0-alpha.4`.

Implement the same truthful ownership contract for:

```text
DefaultForeground
DefaultBackground
TextCursor
MouseForeground
MouseBackground
HighlightBackground
HighlightForeground
```

Prove identity isolation, nesting, out-of-order release, and common/extended-tier behavior without terminal-brand inference.

### T144 — lifecycle failure and concurrency hardening

**Expected version:** `0.14.0-alpha.5`.

Exercise:

- cancellation before baseline query emission;
- cancellation after query emission;
- acquisition queued behind another active query;
- suspend while acquisition/release is pending;
- lifecycle interruption during post-resume re-observation;
- malformed/late color replies;
- partial/failed mutation and restoration writes;
- nested lease churn;
- concurrent palette and dynamic-color ownership;
- session disposal with outstanding leases;
- aggregate cleanup failures;
- no deadlock between output serialization, lifecycle gate, and query transaction manager.

### T145 — bounded terminal protocol closure

**Expected version:** `0.14.0-alpha.6`.

Audit concrete protocol gaps exposed by T140–T144 and implement only those that:

1. strengthen an existing 0.14 ownership/lifecycle contract;
2. are not assigned to 0.15, 0.16, or 0.17;
3. have a semantic typed API or remain internal plumbing;
4. can be bounded and deterministically tested;
5. do not create a generic raw-protocol escape hatch.

If no qualifying gap exists, T145 SHALL document that result and add no feature merely to consume the tranche number.

### T146 — composition and downstream acceptance

**Expected version:** `0.14.0-alpha.7`.

Prove color ownership composes with:

- ordinary application text;
- presentation leases;
- focus/paste/mouse protocol leases;
- cursor style;
- synchronized output;
- progress;
- pointer shape;
- OSC 7 location;
- OSC 8 hyperlinks;
- OSC 52 clipboard;
- OSC 133 core semantic markers;
- active terminal queries.

Extend downstream `Icod.DCurses` acceptance so a real consumer can acquire/release color ownership through public APIs without raw OSC parsing or private lifecycle handling.

### T147 — 0.14 public API/package/stable closure

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

## 8. Testing requirements

At minimum 0.14 SHALL test:

- exact baseline query before mutation;
- no mutation when baseline observation fails;
- exact observed-color replay on final release;
- reset controls never substituted for restoration;
- nesting and out-of-order release;
- independent color identities;
- 16-bit color preservation through baseline capture/replay;
- invalidation and physical-state uncertainty;
- suspend restore → external change → resume re-observe → owned-state reapply;
- session disposal with active leases;
- late lease disposal;
- cancellation/timeout distinction;
- malformed correlated replies;
- late response ownership;
- unrelated application input preservation;
- output/query/lifecycle lock ordering;
- existing presentation/input/cursor/synchronized-output/pointer lifecycle regression suite;
- Windows, Linux, and macOS CI;
- net8.0, net9.0, and net10.0 package-only consumers;
- real downstream `Icod.DCurses` acceptance.

---

## 9. Compatibility policy for target frameworks

The 0.14 release retains:

```text
net8.0
net9.0
net10.0
```

These targets are part of the intended pre-1.0 and 1.0 compatibility surface. Vendor end-of-support dates alone do not cause their removal from Icod.Terminal. A TFM may be reconsidered when a concrete security alert, security fix incompatibility, or equivalent security-maintenance issue makes continued support unsafe or misleading.

CI and package validation SHALL continue treating all three TFMs as first-class targets.

---

## 10. Explicit non-goals

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

## 11. Current development state

```text
VersionPrefix:   0.14.0
VersionSuffix:   alpha.1
Version:         0.14.0-alpha.1
PackageVersion:  0.14.0-alpha.1
AssemblyVersion: 0.14.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** T140 — freeze the lifecycle/query ordering and truthful color-ownership contract before implementing leases.
