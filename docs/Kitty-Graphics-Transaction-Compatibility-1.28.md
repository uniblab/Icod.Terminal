# Kitty Graphics transaction compatibility — Icod.Terminal 1.28

This record separates the published Kitty Graphics Protocol, reviewed upstream
implementation behavior, deterministic transport tests, and operator-visible
rendering. It is not a terminal-brand support promise. Runtime routing uses
capability and transaction evidence, never a terminal name or version heuristic.

## Pinned authorities

- Published protocol source:
  [`docs/graphics-protocol.rst`](https://github.com/kovidgoyal/kitty/blob/96693f4c090e9477b51ffa46aed4abdcef52d037/docs/graphics-protocol.rst)
  at source commit `96693f4c090e9477b51ffa46aed4abdcef52d037`
  (document blob `1473d807edd602e50d6b6f8834967bf18964e996`).
- Implementation under qualification:
  [Kitty 0.49.2](https://github.com/kovidgoyal/kitty/releases/tag/v0.49.2).
- Upstream defect record:
  [kitty #10599](https://github.com/kovidgoyal/kitty/issues/10599).
- Post-0.49.2 correction:
  [`b493a637b0573997f00423e8454f91a966ac96de`](https://github.com/kovidgoyal/kitty/commit/b493a637b0573997f00423e8454f91a966ac96de).

The pinned protocol says animation-frame continuation chunks carry `a=f`, `m`,
and optional `q`; they do not repeat the image id. A nonzero image id on the
initial data command requests a response after transfer. Quietness `q=1`
suppresses success and `q=2` also suppresses errors. The text defines animation
controls under `a=a` without promising a success reply for each control. For
composition `a=c`, defined error paths return correlated failures; the reviewed
portable minimum does not require a successful `OK` reply.

Icod.Terminal therefore keeps the published continuation grammar. It does not
repeat an image id on the final chunk as a Kitty 0.49.2 workaround.

## Action and completion matrix

| Action | Published response policy | Kitty 0.49.2 observation | Successful Icod.Terminal result | Silence or failure |
| --- | --- | --- | --- | --- |
| Frame transfer or regional edit, `a=f` | Required correlated response | A final multi-chunk continuation without a repeated image id stores the frame but loses response identity and sends no acknowledgement. This is the implementation deviation corrected by `b493a63`. | `ProtocolAcknowledged` after correlated `OK` | A correlated error is definite failure. Silence after commitment is ambiguous; append also loses sequence certainty. No frame token is published. |
| Timing, selection, and playback control, `a=a` with `q=2` | No response | Successful controls are silent. | `OutputCommitted` after the complete command is written and flushed | A precommit error fails. A postcommit transport exception remains ambiguous and is not retried automatically. |
| Frame composition, `a=c` with default `q=0` | Optional success response; defined failures remain meaningful | Kitty 0.49.2 sends `OK` on success and correlated errors on documented failure paths. | Correlated `OK`: `ProtocolAcknowledged`; committed silence at the bounded response deadline: `OutputCommitted` | A correlated error is definite failure. Silence is successful output commitment without protocol-acknowledgement evidence. |

The public confirmation values describe evidence available when the call returns:

- `Unspecified` means success strength was not classified. It preserves the
  compatibility behavior of existing local/native mutations and the original
  `TerminalControlMutationResult.Success()` factory.
- `OutputCommitted` means the complete command crossed the serialized write and
  flush boundary. It does not mean the terminal parsed, applied, or rendered it.
- `ProtocolAcknowledged` means a correlated protocol success response was parsed.
  It implies output commitment, but still does not establish visible rendering.

Failures do not publish a confirmation value. A timeout or exception after bytes
may have committed is reported as ambiguous; callers must not blindly replay the
mutation. Resource cleanup remains explicit and deterministic.

## Evidence lanes

| Lane | Current 1.28 record | What it can establish |
| --- | --- | --- |
| Headless scripted transport | Covered by deterministic source tests and the sample's `--headless-transcript` mode | Exact bytes, response/no-response policy, correlation, completion strength, ambiguity, ownership, and cleanup. It never establishes rendering. |
| Kitty 0.49.2 | Reviewed limited live result: small single-chunk frame/control sequence completed; the documented multi-chunk `a=f` append timed out with controlled ambiguity. | The recorded scenarios only: single-chunk transfer/control completion and the exact negative multi-chunk acknowledgement result. It does not establish ATLAS. |
| Source-built Kitty `96693f4c090e9477b51ffa46aed4abdcef52d037`, containing `b493a63` | Reviewed live sample results: default animation and 1/4/16/64 tile-atlas transactions completed; the corrected eight-row witness passed on Terminal source `70c6cac`; the scoped DCurses compose-publish run passed on downstream head `d9ae518`. | Positive evidence for the exercised documented frame transfers, edits, composition, controls, grid layout, cleanup, opaque tile artwork, movement and viewport scrolling. The recorded scope does not include every interactive command. |
| Another Kitty-protocol implementation | `NotRun` | Portability check that behavior does not depend on a Kitty brand/version branch. |
| Operator-visible rendering | Original 0.49.2 lane: small-sample flash. Corrected source build: changing colors in the small sample, the earlier atlas strip, the corrected eight-by-eight Terminal grid, and the downstream DCurses opaque tile artwork with responsive movement and scrolling. | The recorded visual observations only. Transaction confirmation does not claim rendering, atomicity, or gapless presentation; unobserved interactions remain unqualified. |

`NotRun` is evidence, not a synonym for unsupported or failed. Missing,
unreviewed, or differently scoped observations remain `NotRun`; source review and
headless transport success are never promoted to a rendering result.

## Reviewed Kitty 0.49.2 / WSL live witness — 2026-10-08

This is a limited maintainer-run witness, recorded here rather than promoted to
a support claim. The reported environment was direct Kitty in WSL2, with
`TERM=xterm-kitty` and `KITTY_WINDOW_ID=1`; no terminal multiplexer or pipe
was in the transaction path. The available environment capture records Kitty
0.49.2, Ubuntu 24.04 on Linux 6.6.87.2-microsoft-standard-WSL2 x86_64, and
.NET SDK 10.0.112. The precise start time and Kitty configuration-file state
were not separately recorded and therefore remain `NotRecorded`.

| Scenario | Exact Terminal source | Command / outcome | Visible observation | Scope limit |
| --- | --- | --- | --- | --- |
| Small animation control | `187ee56ed617468e43fc1dcab446e7adb202b67b`; Release, `net10.0` | Default raster-animation sample exited 0. Frame replacement and composition were `ProtocolAcknowledged`; timing and all exercised `a=a` controls were `OutputCommitted`; cleanup completed. | Brief color flash observed by the operator. | The frames are single-chunk. This does not qualify multi-chunk append, ATLAS, or an arbitrary renderer. |
| Multi-chunk atlas negative | `40271730750ae3a8da50cc739f58a341143ae125`; Release, `net10.0` | `--tile-atlas` reached the frame append deadline. It printed `Tile-atlas text fallback`, `Status=Ambiguous`, and exited 1 with no stack trace. | `Rendered=NotClaimed`; no rendering assertion is drawn from the output. | This confirms only the documented 0.49.2 required-ACK failure and controlled fallback. It is not an ATLAS pass. |

The negative lane preserves the published `a=f` continuation grammar and does
not retry or repeat the image id. The observed timeout means output may have
committed, but the missing correlated acknowledgement leaves the append
sequence uncertain and no frame token is published.

## Reviewed corrected-source Kitty / WSL witness — 2026-10-08

The maintainer built Kitty from upstream `master` at
`96693f4c090e9477b51ffa46aed4abdcef52d037` in
`/mnt/c/Users/unibl/Development/kitty`. The submitted `git log` and
`kitty/graphics.c` excerpt show the corrected continuation identity recovery from
the starting command. The launched executable was `./kitty/launcher/kitty`.
Although its version output is still `0.49.2`, this is a **post-release source
build containing `b493a63`**, not the unpatched stable 0.49.2 negative lane.

After converting `dev.sh` and `shell-integration/bash/kitty.bash` to LF, the
maintainer launched that executable through Ubuntu-24.04 WSL from Windows and ran
both samples directly in the new Kitty window, with no headless option, pipe or
output redirection. Mesa/EGL and desktop-service warnings remained in the launcher
shell; the recordings nevertheless show completed graphics transactions and
visible changing colors. No universal inference about those warnings is made.

| Scenario | Command / transaction result | Separately reviewed visual observation |
| --- | --- | --- |
| Tile-atlas transaction witness | `dotnet run --project ./samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -c Release -f net10.0 -- --tile-atlas`; all 1/4/16/64-region workloads returned `Available` / `ProtocolAcknowledged`, and their completed-frame selections returned `Available` / `OutputCommitted`. The final witness reported `Verified / LiveObservation` RGB24 evidence. No fallback or exception appeared. | A colored horizontal strip appeared, changed, and disappeared during cleanup. The tested sample emitted all 64 placeholder cells consecutively; this recording does not establish an 8-by-8 physical grid. |
| Small animation witness | Same command without `--tile-atlas`; partial replacement and composition returned `ProtocolAcknowledged`; timing, selection, loading, finite/indefinite playback and stops returned `OutputCommitted`; resource cleanup completed; captured exit status was `0`. | A colored image visibly changed during playback and disappeared during cleanup. The recording does not separately prove the visual correctness or precise timing of each individual mutation. |

Reviewed captures: `20261008-1415-07.2253538.mp4` (atlas) and
`20261008-1430-53.9447193.mp4` (small animation), supplied by Timothy J. Bruce.
The atlas capture does not show the completed exit-status command. The exact
Icod.Terminal checkout SHA, runtime/SDK version for these two runs, precise UTC
start time, and clean/default Kitty configuration state were not independently
captured and remain `NotRecorded`. Both runs precede the sample row-layout fix.
This is therefore a limited positive live checkpoint, not complete T2810
provenance or a downstream ATLAS support claim. The unpatched 0.49.2 negative
witness above remains valid. Other implementations remain `NotRun` under this checkpoint. The later downstream
record below accepts artwork, movement, and scrolling only; help, resize, and
independent exit were not observed in this earlier run.

## Reviewed eight-row retest — 2026-10-08

The maintainer supplied the exact Terminal checkout commit
`70c6cacd2d26c3f962db513a23035f2570060cea` on
`docs/1.28.0-kitty-graphics-roadmap`. The recording shows the matching repository
path `/mnt/c/Users/unibl/Development/Icod/Icod.Terminal`, SDK `10.0.112`, and
pre-run `date -u` output `Thu Oct 8 15:11:42 UTC 2026`. That timestamp precedes
the build; it is not asserted as the precise sample-process start time. This
continues the previously reported source-built Kitty / Ubuntu-24.04 WSL lane;
Kitty source and configuration were not independently recaptured in this clip.

The direct, unredirected command was:

```text
dotnet run --project ./samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj -c Release -f net10.0 -- --tile-atlas
```

The recording shows an eight-by-eight cell grid with changing colors, with all
operation reports below it. All 1/4/16/64-region damage workloads returned
`Available` / `ProtocolAcknowledged`; their completed-frame selections returned
`Available` / `OutputCommitted`; the final RGB24 operation evidence was
`Verified / LiveObservation`. The grid disappeared during cleanup, the Bash
prompt returned, and the immediately following exit-status command printed `0`.
No fallback or exception appeared. This accepts the corrected sample layout and
the exercised transaction/rendering/cleanup path, without claiming remote
atomicity, gapless presentation or complete visual validation of every tile edit.

Reviewed capture: `20261008-1512-07.0969947(1).mp4`, supplied by Timothy J. Bruce,
SHA-256 `69452c56dec4cd6a44e6f6ac3bb9f3c5cca17fe2a7c18405f5f0e8ee0dcf477a`.
The exact runtime patch, clean working-tree state, Kitty configuration and
precise sample-process start remain `NotRecorded`. Other implementations remain
`NotRun` under the new contract; the downstream evidence below is limited to its
recorded compose-publish and interaction subset.

The tested Terminal source passed all ten jobs in
[workflow 37796031515](https://github.com/uniblab/Icod.Terminal/actions/runs/37796031515),
including .NET 8/9/10 runtime checks on Windows, Linux and macOS and every package
gate. The first Linux attempt hit a deadline in the existing ambiguous-pixel
hardening test's follow-up append on .NET 8; its .NET 9/10 runs passed. The same-head
Linux retry passed. The local .NET 8 and 10 suites each passed all 2,868 unit tests;
the local .NET 10 TermInfo integration suite passed all 15 tests.

## Reviewed downstream DCurses ATLAS acceptance — 2026-10-09

The accepted downstream source is Icod.DCurses branch
`2.3.0-raster-atlas-roadmap` at exact head
[`d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d`](https://github.com/uniblab/Icod.DCurses/commit/d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d),
using the published `Icod.Terminal 1.28.0-alpha.1` dependency. Its
[workflow 37940789076](https://github.com/uniblab/Icod.DCurses/actions/runs/37940789076)
passed all seven jobs. The maintainer separately reported all 1,366 local
`net10.0` tests passing.

In the reviewed corrected-source Kitty lane, the DCurses RasterAtlas sample
showed the supplied opaque 16-by-16 water, grass, forest, road, and player artwork.
Movement remained responsive, and viewport scrolling completed at the tested
area boundary after full-coverage updates were coalesced. This accepts the
exercised retained-renderer compose-publish path and those operator observations.
It does not turn `OutputCommitted` or `ProtocolAcknowledged` into rendering
claims, and it does not establish atomic or gapless presentation.

The evidence scope is intentionally narrower than every sample interaction:
help, resize, and independent exit observations remain `NotRun`. Other
Kitty-protocol implementations remain `NotRun`. FRAME and TEXT remain controlled
alternatives rather than evidence for persistent ATLAS.

## Required provenance for a live report

Every reviewed live result records all of the following:

| Field | Required value |
| --- | --- |
| Scenario | Stable scenario name and revision; operation sequence and bounded command used |
| Terminal | Exact implementation name, version, build/source commit, and whether the fix commit is present |
| Platform | OS name/version/architecture and relevant distribution details |
| Host and transport | Native/PTY/WSL/container path, host application/version, and endpoint configuration |
| Icod.Terminal | Package version or exact source commit, target framework, build configuration, and runtime version |
| Time | UTC start time and bounded timeout/deadline settings |
| Transaction result | Operation, status, `Unspecified`/`OutputCommitted`/`ProtocolAcknowledged`, fallback reason, and cleanup outcome |
| Evidence | Evidence kind, source, generation/scope, raw report location, and reviewer |
| Visible result | Separately recorded `Rendered`, `Failed`, `Unavailable`, or `NotRun`, including the operator observation; never inferred from confirmation |

The runnable public-only walkthrough is
[`../samples/Icod.Terminal.RasterAnimation.Sample/README.md`](../samples/Icod.Terminal.RasterAnimation.Sample/README.md).
The approved contract and its evidence boundaries are in the
[`1.28 design`](superpowers/specs/2026-10-06-1.28.0-published-spec-kitty-graphics-transactions-design.md)
and the [`1.28 roadmap`](../Icod.Terminal-1.28.0-Development-Roadmap.md).
