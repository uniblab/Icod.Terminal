# Graphics development hold

Hold decision date: 2026-10-04.

Bounded reopening date: 2026-10-06. The maintainer reopens graphics development
only for the Icod.Terminal 1.28 published-spec transaction and downstream ATLAS
scope recorded below. This file preserves the reasons for the original hold and
the evidence that changed the decision. Unselected graphics expansion remains
deferred.

## Preserved baseline

- Stable `1.27.0` is merged and tagged at
  `485bedb014ab7b66391661c709833a6a4fa4afcc`; the maintainer reports NuGet upload
  is in progress. Earlier 1.24.1/1.25 alpha evidence remains historical.
- Preserve existing APIs, raster capability selection, protocol encoding,
  acknowledgement/cancellation rules, lifecycle certainty, deadlines and cleanup.
- Retain alpha.5's bounded size-aware persistent transfer deadlines, alpha.4's DA1
  parsing correction, alpha.3's immediate Unix byte input, and the earlier cursor
  and coalesced Sixel corrections.
- The 1.28 planning decision does not itself change runtime code, public API,
  dependencies or package version. T2800 requires a reviewed detailed design and
  implementation plan before runtime work begins.

## Confirmed evidence and remaining limits

In the maintainer's WSL2 / Ubuntu 24.04 tests, the independent two-pixel Bash
reproducer on Kitty 0.32.2 and later Kitty 0.49.2 receives root and single-chunk
animation `OK` replies, no reply for the documented chunked `a=f,m=0` final
continuation, and `Gi=1,r=4;OK` when that final continuation repeats the image
identifier. Frame 4 establishes that the silent upload created frame 3. Source
inspection agrees with a response-identity defect in 0.49.2. These observations
do not establish live success on a corrected build or clean configuration.

The durable [reproducer](https://github.com/uniblab/Icod.DCurses/blob/a73c290eeb9bdec4dce614f5a15e4d21c1382108/tools/kitty-frame-ack.sh)
and [bug-report draft](https://github.com/uniblab/Icod.DCurses/blob/a73c290eeb9bdec4dce614f5a15e4d21c1382108/tools/kitty-frame-ack-bug-report.md)
record captured output and source evidence. An initial integration filing attempt
returned HTTP 403. The maintainer subsequently filed
[kitty issue #10599](https://github.com/kovidgoyal/kitty/issues/10599) directly.
Upstream closed it on 2026-10-04 after commit
[`b493a637b0573997f00423e8454f91a966ac96de`](https://github.com/kovidgoyal/kitty/commit/b493a637b0573997f00423e8454f91a966ac96de)
recovered the starting image identity for explicit `a=f` continuations. That fix
postdates stable Kitty 0.49.2; live validation on a containing build remains
required.

Terminal's bounded transfer deadline does not resolve the 0.49.2 defect. In the
historical unpatched stable-0.49.2 lane, the downstream default DCurses sample
abandoned atlas setup after its deadline and used ordinary FRAME. Forced TEXT
worked; ordinary FRAME had correct input and display with implementation-dependent
flicker. Those observations do not qualify persistent ATLAS. The later
corrected-source lane and its scoped downstream acceptance are recorded below.

Animation-control response assumptions remain a separate concern. The published
protocol documents `a=a` controls but does not unambiguously promise a success
response for every control. Kitty 0.49.2 and the reviewed current implementation
execute those controls without a success reply, and upstream tests expect no
response. This is recorded as implementation behavior in a specification
ambiguity, not presently asserted as a protocol violation. Kitty also emits `OK`
for successful `a=c` composition even though the reviewed text is explicit about
specified failure responses; that stronger behavior must not become a universal
portable requirement without the 1.28 reference review.

## Fallback boundary

Ordinary `RasterGraphics` selection prefers verified Kitty, then verified Sixel.
Terminal sends through one selected backend; it does not replay a failed Kitty
write through Sixel. Applications own text fallback and durable source pixels.

Persistent Kitty resource, placeholder and animation identity remains separately
verified and is not emulated in Sixel. A DCurses atlas setup failure can therefore
fall back to **ordinary Kitty FRAME**, rather than selecting Sixel. Persistent
animation uncertainty still requires caller-owned recovery under existing contracts.

## Deferred work

The 1.28 reopening authorizes only published-spec animation transaction semantics,
truthful confirmation strength, focused probe/sample coverage and downstream ATLAS
acceptance. It does not authorize a Kitty 0.49.2 repeated-id workaround or a
terminal-brand/version-specific encoder.

The existing expansion candidates remain deferred: bounded frame-edit batching,
Indexed8 regional parity, gapless frames, expanded placement, image codecs, hidden
replay and broader terminal-side reconciliation. Do not silently revive them
through implementation or release preparation.

Input, lifecycle, screen planning, profile/capability and operational-protocol work
may be considered independently under a separately selected scope. The graphics
hold does not remove or disable existing public APIs or regress unrelated behavior.

## Reopening and release disposition

### Selected specification-first reopening — 2026-10-06

The maintainer approved Icod.Terminal 1.28 as a bounded reopening after the
upstream issue and correction established that the documented chunk grammar was
sound and the missing response was an implementation defect. The selected policy
is:

- emit the published Kitty Graphics Protocol rather than a release-specific
  dialect;
- wait only for responses guaranteed by the reviewed published contract;
- distinguish local output commitment from correlated protocol acknowledgement;
- record implementation extensions and deviations as exact compatibility evidence;
- avoid terminal-brand/version heuristics and the repeated-image-id continuation
  workaround;
- qualify each persistent ATLAS claim only through recorded downstream live evidence at the claimed scope;
- retain controlled FRAME/TEXT fallback for unqualified implementations.

The [1.28 development roadmap](../Icod.Terminal-1.28.0-Development-Roadmap.md)
defines T2800–T2810. T2800 must pin the action-by-action response matrix, freeze
any additive public confirmation model and produce a reviewed implementation plan
before runtime code changes.

### Approved DA1 parser follow-up — 2026-10-06

The maintainer revisited the existing graphics path on Kitty 0.49.2 through WSL.
The independent two-pixel reproducer again received root and single-chunk `OK`
replies, no reply for the documented `a=f,m=0` continuation, and frame-4 `OK` when
the final continuation repeated the image identifier. This reproduces the missing
upload acknowledgement on that exact version; clean-config provenance was not
captured in the submitted screenshot. No upstream resolution is established.

The persistent-raster sample at stable 1.26 source `2c0fafaf` separately failed
before resource creation while parsing Primary Device Attributes. The captured
reply was `CSI ?62;52;c`. The existing special case accepted an empty attribute
list (`?62;c`) but rejected a trailing separator after populated attributes.
The maintainer approved the narrow DA1 parser correction for PR #83 / 1.27.0.
It preserves all numeric attributes and bounds, rejects interior empty fields,
and does not alter other CSI grammars, graphics wire encoding, ACK assumptions,
fallback selection or public signatures. This approval is limited to that parser
correction; persistent ATLAS and animation-control live acceptance remain deferred.

### Post-correction persistent-resource result — 2026-10-06

After updating to the corrected 1.27 PR source, the maintainer reran
`Icod.Terminal.PersistentRaster.Sample` under Kitty 0.49.2 / WSL with `Release`
and `net10.0`. The sample briefly displayed its generated colors, completed every
resource/placement update and ownership message, and returned normally to the shell.
The brief display is expected because the sample has no interactive pause and
deterministically removes its remaining placements and resources on exit.

This result qualifies the exercised persistent resource, physical/relative placement,
update, ownership-observation and cleanup path after the DA1 correction. The sample
does not append, compose, edit or select animation frames, so this observation does
not qualify persistent ATLAS or alter the missing-ACK evidence above.

The original reopening criterion is now satisfied only for the selected 1.28
scope. Clean/default-configuration observations and actual upload, control and
rendering results are still required before making stronger support claims;
controlled-failure and uncertainty semantics remain in force.

The historical 1.25 release preparation retained the hold limits; see
[the closure record](T2508-1.25-Stable-Source-Closure.md). Stable 1.27 is now the
1.28 baseline. The bounded reopening does not retroactively qualify any earlier
persistent path, and Icod.DCurses must consume the accepted 1.28 prerelease or
exact source before its ATLAS result can qualify the new contract.

### Implemented transaction boundary — 2026-10-07

The selected 1.28 source now implements the approved action-specific policy. It
keeps documented `a=f` continuation bytes and required acknowledgement, completes
`a=a` controls after serialized write/flush with `q=2`, and treats successful
`a=c` acknowledgement as optional while retaining correlated failure handling.
The public result distinguishes `Unspecified`, `OutputCommitted`, and
`ProtocolAcknowledged`; none claims rendering.

Deterministic transport, lifecycle, API, package, and sample gates are green on
Windows, Linux, and macOS. This source evidence does not qualify a live terminal
or persistent ATLAS. Kitty 0.49.2 remains the documented negative multi-chunk
frame-acknowledgement lane. The later corrected-source checkpoint below covers
the exercised Terminal samples; another Kitty-protocol implementation remains
`NotRun`. The exact matrix and required provenance are in the
[1.28 transaction compatibility record](Kitty-Graphics-Transaction-Compatibility-1.28.md).

### Corrected-source live checkpoint — 2026-10-08

The maintainer built and launched Kitty source
`96693f4c090e9477b51ffa46aed4abdcef52d037`, containing the upstream `b493a63`
continuation-identity fix, through Ubuntu-24.04 WSL. Its version string still says
`0.49.2`; the source commit distinguishes it from the unpatched stable release.
The default Terminal animation sample completed with exit status 0, acknowledged
regional replacement and composition, output-committed controls, visible changing
colors and completed cleanup. The tile-atlas witness completed all 1/4/16/64
acknowledged RGB24 damage workloads and output-committed selections, showing a
changing horizontal strip before cleanup.

This removes the missing frame-ACK blocker for those exercised source-build
transactions. It does not invalidate the negative stable-0.49.2 witness, establish
clean/default configuration, or by itself qualify a physical 8-by-8 grid or the
DCurses retained renderer. The recordings do not include the exact Terminal
checkout SHA or all required live-provenance fields. See the compatibility record
for the capture names, missing fields and precise observation limits. The later
2026-10-09 checkpoint below records the separately accepted downstream subset.

The later `20261008-1512-07.0969947(1).mp4` retest on exact Terminal source
`70c6cacd2d26c3f962db513a23035f2570060cea` accepts the corrected eight-by-eight
sample grid: colors change, reports stay below it, all 1/4/16/64 workloads complete,
cleanup removes the image and the process exits 0. The capture records SDK
`10.0.112` and pre-run UTC time `2026-10-08 15:11:42`. All ten jobs in workflow
`37796031515` passed on the tested source after the same-head Linux retry. This
supersedes the pending Terminal grid-layout check while retaining the missing
runtime/configuration provenance and portability limits.
### Scoped downstream ATLAS closure — 2026-10-09

The downstream acceptance source is Icod.DCurses branch
`2.3.0-raster-atlas-roadmap` at exact head
[`d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d`](https://github.com/uniblab/Icod.DCurses/commit/d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d),
using `Icod.Terminal 1.28.0-alpha.1`. Its
[seven-job workflow 37940789076](https://github.com/uniblab/Icod.DCurses/actions/runs/37940789076)
passed, and the maintainer reported all 1,366 local `net10.0` tests passing.

The reviewed corrected-source Kitty run displayed the supplied opaque 16-by-16
water, grass, forest, road, and player artwork. Movement remained responsive and
the viewport scrolled at the tested area boundary after the full-coverage update
coalescing correction. This closes the selected 1.28 downstream gate for that
exercised compose-publish and interaction subset. It does not claim terminal-side
atomicity, gapless presentation, or portability to an untested implementation.

The remaining observation boundary is explicit: help, resize, and independent exit observations remain `NotRun`.
Other Kitty-protocol implementations remain `NotRun`. FRAME and TEXT retain their
controlled-alternative roles. Unrelated graphics expansion listed above remains
on hold.
