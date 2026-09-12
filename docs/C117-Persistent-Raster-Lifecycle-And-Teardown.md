# C117 — Persistent Raster Lifecycle and Teardown Acceptance

**Project:** `Icod.Terminal`  
**Release track:** `1.11.0`  
**Tranche:** C117 — lifecycle invalidation, teardown, and failure semantics  
**Accepted code head:** `aec40cf9d9c2cfcccd1ccea15c315a7558c45715`  
**Qualification workflow:** `#1575 / 34692497521`  
**Result:** accepted

## Accepted semantics

C117 integrates persistent raster ownership with the existing session lifecycle without adding any new public API.

The accepted implementation establishes:

- generation-scoped persistent resource and placement identity;
- `TerminalSession.InvalidateState()` invalidation of persistent raster certainty through presentation-state invalidation;
- stale resource placement creation returning controlled `Unavailable` before output;
- stale placement update returning controlled `Unavailable` before output;
- stale placement/resource disposal performing local cleanup only, without emitting stale Kitty numeric identities;
- managed suspend/resume and external resume invalidating existing persistent handles without raster replay or re-upload;
- no retained raster source cache for lifecycle restoration;
- post-acknowledgement resource creation rechecking generation ownership before publishing a public handle;
- placement creation rechecking generation ownership before publishing a public handle;
- session teardown atomically draining current persistent ownership;
- all current placements being deleted before any current resource data is deleted;
- deterministic ordering within the placement and resource cleanup groups;
- cleanup continuing after individual persistent-raster transport failures;
- persistent-raster cleanup failures participating in the existing presentation/session aggregate-disposal model;
- handle disposal using the internal control-output gate so teardown can stop new public output while still serializing cleanup safely.

## No replay contract

C117 does not replay, re-upload, or rebind persistent raster content after explicit invalidation, suspension, resume, or other generation changes. Existing handles become stale and remain stale. Applications must create new resources explicitly when they need new terminal-resident ownership.

## Compatibility and API

C117 is internal/behavioral only. It adds no public members and does not alter the C116 public API freeze.

The current 1.11 deterministic public API fingerprint remains:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

Supported target frameworks remain:

- `net8.0`
- `net9.0`
- `net10.0`

## Verification evidence

Exact head `aec40cf9d9c2cfcccd1ccea15c315a7558c45715` passed pull-request workflow `#1575 / 34692497521` unchanged.

Successful jobs included:

- Runtime Windows
- Runtime Linux
- Runtime macOS
- Package candidate / public API freeze
- Package Foundation
- Package Presentation
- Package Semantic and hardening
- Package Stable 1.x release line
- Validated package artifact

The corrected C117 RED checkpoint first demonstrated the missing lifecycle behavior while the package/API gates stayed green, and the GREEN exact head subsequently passed the complete matrix.

## Next gate

C118 owns adversarial hardening, repeated-lifecycle and ownership stress, terminal-side disappearance handling, fresh-package consumer qualification, downstream `Icod.DCurses` acceptance, and the focused backend-neutral persistent-raster sample.
