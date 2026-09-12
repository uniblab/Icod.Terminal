# C117 — Persistent Raster Lifecycle Acceptance

**Project:** `Icod.Terminal`  
**Release track:** `1.11.0`  
**Accepted code head:** `aec40cf9d9c2cfcccd1ccea15c315a7558c45715`  
**Qualification workflow:** `#1575 / 34692497521`  
**Result:** accepted

## Scope

C117 integrates persistent raster ownership with session invalidation, managed suspend/resume, and session teardown without adding public API.

Accepted behavior includes:

- generation-scoped resource and placement identity;
- explicit `InvalidateState()` staling existing persistent handles;
- managed suspend/resume and external resume staling existing persistent handles without replay or re-upload;
- stale resource placement creation returning controlled `Unavailable` before output;
- stale placement update returning controlled `Unavailable` before output;
- stale placement/resource disposal performing local cleanup only, without stale protocol identifiers;
- resource and placement creation rechecking generation ownership before publishing a public handle;
- current-generation session teardown deleting every placement before any resource data;
- deterministic cleanup ordering;
- cleanup continuation after individual persistent-raster transport failures;
- aggregation of persistent-raster cleanup failures into the existing session/presentation disposal model;
- use of the internal control-output gate for cleanup after public session output has been closed.

No source raster is retained and no automatic restoration/rebinding is introduced.

## API compatibility

C117 is behavioral/internal only. The 1.11 public API remains the C116 surface with deterministic fingerprint:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

Supported target frameworks remain `net8.0`, `net9.0`, and `net10.0`.

## Verification

Exact code head `aec40cf9d9c2cfcccd1ccea15c315a7558c45715` passed pull-request workflow `#1575 / 34692497521` unchanged.

Successful jobs:

- Runtime Windows
- Runtime Linux
- Runtime macOS
- Package candidate / public API freeze
- Package Foundation
- Package Presentation
- Package Semantic and hardening
- Package Stable 1.x release line
- Validated package artifact

C117 is accepted. C118 owns adversarial hardening, terminal-side disappearance handling, repeated lifecycle/ownership stress, fresh-package consumer qualification, downstream `Icod.DCurses` acceptance, and the focused backend-neutral persistent-raster sample.
