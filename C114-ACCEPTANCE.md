# Icod.Terminal 1.11.0 C114 Acceptance

**Tranche:** C114 — persistent resource creation/upload  
**Accepted head:** `5a4e8860322eb18505e2d77baa18c3a81d93b7af`  
**Pull-request workflow:** run `34659060708` / `#1561`  
**Configuration:** `Staging`

C114 is accepted because the exact unchanged head above passed the full pull-request matrix on Windows, Linux, and macOS, including package candidate construction, every package-contract shard, and the validated package artifact.

## Accepted contract

C114 introduces the first public persistent-raster ownership operation while preserving the C110 opacity boundary.

The accepted behavior is:

- `TerminalSession.CreateRasterResourceAsync(...)` requires current verified `PersistentRasterGraphics` capability and performs no hidden support probe;
- Sixel is never used as a persistent-resource fallback;
- resource capacity is reserved in the bounded C113 registry before protocol output begins;
- persistent Kitty direct upload uses the private nonzero image number and publishes no public resource until a matching acknowledgement is received;
- successful acknowledgement binds the terminal-assigned nonzero image id privately;
- well-formed `ENOENT` creation response returns controlled `Unavailable`;
- other well-formed Kitty negative responses return controlled `Failed`;
- duplicate, overflowing, or otherwise malformed correlated acknowledgement fields surface `FormatException`;
- response correlation can route a malformed matching frame without terminating the shared input pump; strict bounded validation occurs immediately after routing;
- post-commit caller cancellation does not truncate the remaining upload frames;
- canceled/timed-out committed transactions retain local identity ownership until the existing late-response ownership interval ends, after which rollback releases the reservation;
- failed or ambiguous creation never publishes a usable `TerminalRasterResource`;
- the registry continues to retain no source `TerminalRasterImage` or pixel payload after creation completes.

The accepted C114 public API fingerprint is:

```text
fbef4700d29613be7f6224a5349426c731412a851127bd529fc190ac3a562aeb
```

It is identical across `net8.0`, `net9.0`, and `net10.0` and covers the C112 capability plus the C114 opaque resource/session creation surface.

## TDD evidence

The C114 RED checkpoint was commit `1bd3c04adfed8d5a8faa26ac1b4472b8a2eb37d5`. Runtime compilation failed on all three target frameworks because `TerminalRasterResource` and `TerminalSession.CreateRasterResourceAsync(...)` did not yet exist, with no unrelated warnings.

The initial GREEN implementation was commit `213de5e6ca1eb4a7d05a0b9a5df0f6a707bb97d5`. Qualification then exposed two real contract issues:

1. the interim C112 public API fingerprint correctly rejected the intentional C114 surface addition; and
2. strict persistent-response matching needed to preserve C111 duplicate/overflow validation without throwing inside the shared response-routing pump.

Those issues were corrected while preserving the frozen behavior. Exact head `5a4e8860322eb18505e2d77baa18c3a81d93b7af` then passed workflow `#1561 / 34659060708` across:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

C114 is therefore closed. Placement creation and multi-placement ownership remain C115; placement update and deterministic terminal cleanup remain C116; lifecycle invalidation and teardown remain C117.
