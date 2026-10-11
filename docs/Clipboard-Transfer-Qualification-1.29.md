# Clipboard transfer qualification — 1.29

Candidate: **1.29.0-alpha.1**, implementation in
[PR #87](https://github.com/uniblab/Icod.Terminal/pull/87).
Recorded 2026-10-11 UTC. This is automated/component evidence, not live clipboard
acceptance. Stable promotion and publication remain separate maintainer actions.

## Environment and reproducibility

Linux 6.18.44, x86_64; SDK 10.0.100; runtime 8.0.22, 9.0.11 and 10.0.0.
PowerShell 7.5.4 ran the existing package scripts. This container denies MSBuild
worker sockets, so local builds used `-m:1 -p:UseSharedCompilation=false` with
`DOTNET_EnableDiagnostics=0`. A scratch-only command adapter serializes builds
invoked by the unmodified package scripts. Repository build behavior is unchanged.
These results do not claim a Windows PowerShell 5.1 or live terminal execution.

## Automated evidence

| Check | Result |
| --- | --- |
| Release solution build, all targets | Passed; zero warnings/errors |
| Full unit suite | 2,925 passed per target on net8.0/net9.0/net10.0; zero skipped/failed |
| TermInfo integration suite | 15 passed per target; zero skipped/failed |
| Clipboard help and no-argument startup | Passed headlessly; no terminal opened |
| Compatibility sample fresh-package smoke | Passed on all targets; lists/describes the larger scenario headlessly and preserves historical evidence matrices |
| Header/license gate | Passed through package build |
| Cross-TFM API snapshots | Identical; only the new options type/session property added |
| Fresh package validation | Passed on all three targets, including exact larger write/read bytes through public APIs |
| Existing DCurses package acceptance | Passed on all targets with published DCurses 1.6.0/2.2.0 and Terminal-owned screen contracts |

The additional simultaneous-session budget isolation test passed separately on
all three targets, bringing unique qualified unit cases to 2,926 per target.
Exact GitHub matrix status is authoritative on the PR; local results
do not substitute for unobserved Windows/macOS jobs.

Read/write cases cover 65,537 bytes, 1 MiB and 16 MiB, strict text byte accounting,
same-length base64 blocks crossing a decoded limit, one-byte fragmentation,
BEL/ESC-ST/C1 framing, a 32-byte ordinary input buffer, wrong-selection isolation,
malformed replies, exact overflow boundaries, bounded recovery exhaustion, EOF,
timeout, cancellation, late ownership and disposal. Existing ordering, lifecycle,
ordinary input, paste and unrelated protocol regression tests remain in the suite.

One-byte malformed framing exposed an empty-drain consumption bug and an already
consumed terminator edge case; regression fixtures now exercise both. The decoder
does not consume the next key while searching for a second terminator.

API SHA-256:

```text
3239c1682724f7a4df4b55cf1c29117d23d3bfd88e23c3c8c715a5d48cced517
```

The baseline at `2c410cd80663fef30912079e2c0de7a550852b97` was regenerated and
matched its retained 1.28 fingerprint before reviewing the additive API diff.

## Allocation and work

`TerminalClipboardAllocationTests` measures the synchronous encoder and the
incremental accumulator plus final copy/decoding, excluding generated fixtures
and test assertions. The read measurement deliberately calls Feed once per byte;
it does not repeatedly reparse the accumulated prefix. It measures that component
at all sizes, including 64 KiB, rather than claiming the default session changed
to the opt-in reader. Session-path fragmentation tests separately exercise transport
integration. Elapsed times are observations, never unit-test pass thresholds.

Representative .NET 10 Release run:

| Decoded bytes | Write allocated | Write ms | Incremental read allocated | Read ms | Live response storage bound |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 65,536 | 87,424 | 1.235 | 368,168 | 17.036 | 240,336 |
| 1,048,576 | 1,398,144 | 1.421 | 5,942,024 | 28.582 | 3,844,816 |
| 16,777,216 | 22,369,664 | 18.421 | 95,067,328 | 460.387 | 61,516,496 |

All memory columns are bytes. Allocation totals include arrays that become
collectible during growth; the last column is an analytical bound, **not measured
process RSS**, excluding object headers, ordinary scratch storage and caller/
transport buffers. With `F = 4 * ceil(N/3) + 16`, simultaneously reachable response
storage is bounded by `2F + N`: growth overlaps old/new arrays, or the accumulator
coexists with its exact frame copy and decoded result. Total allocations are bounded
by `4F + N` plus fixed overhead. Old accumulator arrays are cleared on growth and
disposal; completed results/caller buffers follow normal managed ownership.

The write allocation assertion allows 32 KiB fixed CLR bookkeeping slack across
frameworks; it still rejects an additional payload-sized buffer or base64 string.
One .NET 10 full-suite run reported approximately 8 KiB additional accounting at
16 MiB, so a 1 KiB slack threshold was too narrow. Idle options/accumulator and
over-budget rejection tests assert no payload-sized allocation.

A separate warmed encoder comparison used the same 65,536-byte input and exact
87,393-byte output on the original 1.28 source and this implementation:

| Encoder | Allocated bytes | Elapsed ms |
| --- | ---: | ---: |
| 1.28 baseline | 262,216 | 1.943 |
| 1.29 candidate | 87,424 | 2.206 |

The memory reduction is demonstrated; these single timing observations establish
no throughput ranking. Whole-session peak-memory profiling, OS-specific allocation
behavior and transport throughput remain follow-up qualification. Complete-frame
writes still have a non-cancellable post-commit interval; no streaming/zero-copy
claim is made.

## Live and release gates

| Environment | Clipboard scenario | Status |
| --- | --- | --- |
| Windows Terminal | Default, larger transfer, refusal/timeout | NotRun |
| Linux emulator / SSH / multiplexer | Default, larger transfer, refusal/timeout | NotRun |
| macOS emulator | Default, larger transfer, refusal/timeout | NotRun |

No operator clipboard was queried or overwritten during implementation. The
compatibility sample provides consented `clipboard.osc52` and
`clipboard.osc52.large` scenarios; the latter is exactly 131,072 generated ASCII
bytes. A live record must state exact terminal/version, OS, runtime, transport,
source commit and sizes. A timeout is inconclusive, and output emission alone is
not terminal acceptance. Unavailable environments remain NotRun unless the
maintainer explicitly accepts a release exception.

Final GitHub CI, whole-session profiling, any desired live checks and stable
promotion remain visible in the roadmap. Nothing here authorizes merge/tag/publish.
