# Graphics development hold

Decision date: 2026-10-04. The maintainer puts Icod.Terminal graphics development
on hold alongside Icod.DCurses because Kitty development and support cannot
currently be properly tested and qualified.

## Preserved baseline

- Stable Terminal remains `1.24.1`; `1.25.0-alpha.5` is merged and published.
- Preserve existing APIs, raster capability selection, protocol encoding,
  acknowledgement/cancellation rules, lifecycle certainty, deadlines and cleanup.
- Retain alpha.5's bounded size-aware persistent transfer deadlines, alpha.4's DA1
  parsing correction, alpha.3's immediate Unix byte input, and the earlier cursor
  and coalesced Sixel corrections.
- No version bump, runtime change, dependency change, merge or publication is
  implied by this documentation decision. The subsequent instruction to finish 1.25 authorizes stable release preparation with these limits retained; exact-head qualification and publication remain separate steps.

## Confirmed evidence and remaining limits

In the maintainer's WSL2 / Ubuntu 24.04 / Kitty 0.32.2 test, the independent
two-pixel Bash reproducer receives root and single-chunk animation `OK` replies,
no reply for the documented chunked `a=f,m=0` final continuation, and
`Gi=1,r=4;OK` when that final continuation repeats the image identifier.
Frame 4 establishes that the silent upload created frame 3. Source inspection
agrees with an upstream response-identity defect. This does not establish live
success on a newer Kitty version or a clean configuration.

The durable [reproducer](https://github.com/uniblab/Icod.DCurses/blob/a73c290eeb9bdec4dce614f5a15e4d21c1382108/tools/kitty-frame-ack.sh)
and [bug report](https://github.com/uniblab/Icod.DCurses/blob/a73c290eeb9bdec4dce614f5a15e4d21c1382108/tools/kitty-frame-ack-bug-report.md)
record captured output and source evidence. Filing with `kovidgoyal/kitty` was
attempted but returned HTTP 403, `Resource not accessible by integration`.
**No upstream issue was created.** The defect remains unresolved.

Terminal alpha.5 allows more bounded transfer time; it does not resolve the missing
ACK. The downstream default DCurses sample still abandons atlas setup after its
deadline and uses ordinary FRAME. Forced TEXT works; ordinary FRAME has correct
input/display and command-driven flicker. These observations do not qualify
persistent ATLAS. Animation-control ACK assumptions are a separate unqualified
concern. Automated protocol fixtures cannot substitute for live rendering evidence.

## Fallback boundary

Ordinary `RasterGraphics` selection prefers verified Kitty, then verified Sixel.
Terminal sends through one selected backend; it does not replay a failed Kitty
write through Sixel. Applications own text fallback and durable source pixels.

Persistent Kitty resource, placeholder and animation identity remains separately
verified and is not emulated in Sixel. A DCurses atlas setup failure can therefore
fall back to **ordinary Kitty FRAME**, rather than selecting Sixel. Persistent
animation uncertainty still requires caller-owned recovery under existing contracts.

## Deferred work

Further graphics expansion, Kitty-specific fixes/workarounds, ACK assumption changes,
new probe coverage and additional live Kitty acceptance are deferred. The existing
candidate list stays available for later review: bounded frame-edit batching,
Indexed8 regional parity, gapless frames, expanded placement and image codecs.
Do not silently revive these through a release-preparation or documentation task.

Input, lifecycle, screen planning, profile/capability and operational-protocol work
may be considered independently under a separately selected scope. The graphics
hold does not remove or disable existing public APIs or regress unrelated behavior.

## Reopening and release disposition

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

Reopening requires a new maintainer scope decision and an environment where
Kitty graphics behavior can be properly reproduced and tested. Establish
clean-config/current-version observations and actual upload/control/rendering
results before making stronger support claims; retain controlled-failure and
uncertainty semantics until those results justify a reviewed change.

The maintainer subsequently requested completion of stable 1.25.0. Its release
preparation retains these graphics limits and the alpha.5 runtime implementation;
see [the closure record](T2508-1.25-Stable-Source-Closure.md). Exact release-head
qualification, mainline validation and publication remain required. The hold itself neither
accepts the untested persistent path nor forbids release with explicitly documented
limits. Icod.DCurses can retain its existing published alpha.5 dependency while
its own release disposition is reviewed.
