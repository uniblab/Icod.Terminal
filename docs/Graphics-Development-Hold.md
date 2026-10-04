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
  implied by this documentation decision. Stable 1.25 closure remains pending.

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

Reopening requires a new maintainer scope decision and an environment where
Kitty graphics behavior can be properly reproduced and tested. Establish
clean-config/current-version observations and actual upload/control/rendering
results before making stronger support claims; retain controlled-failure and
uncertainty semantics until those results justify a reviewed change.

Stable 1.25 promotion, exact release-head qualification and treatment of the known
graphics limits require a separate release decision. The hold itself neither
accepts the untested persistent path nor forbids release with explicitly documented
limits. Icod.DCurses can retain its existing published alpha.5 dependency while
its own release disposition is reviewed.
