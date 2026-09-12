# C116 — Persistent Raster Update and Deterministic Cleanup

**Release:** `1.11.0-alpha.1`  
**Tranche:** C116  
**Status:** accepted  
**Accepted head:** `db5dab910f818bb1894c906c3915ad30dd608037`  
**Qualification workflow:** `#1571 / 34667244384`

## Accepted contract

C116 completes the approved public persistent-raster ownership surface with semantic placement replacement and deterministic current-generation cleanup.

The accepted behavior provides:

- `TerminalRasterPlacement.UpdateAsync(...)` returning the existing `TerminalControlMutationResult` vocabulary;
- placement replacement at the terminal's current cursor position with Kitty `C=1`;
- replacement reusing the same private image-id / placement-id pair;
- independently optional `Columns` / `Rows`, each bounded to `1..16384`;
- self-disposed placement update throwing `ObjectDisposedException`;
- a placement whose parent/local ownership is no longer current returning controlled `Unavailable` before output;
- placement disposal claiming local ownership exactly once and then attempting one quiet targeted soft delete;
- resource disposal atomically closing child placement ownership, attempting child soft deletes first, then the hard image-data delete;
- quiet cleanup using Kitty `q=2`;
- local identity ownership remaining released even when terminal cleanup transport fails;
- no retry of protocol cleanup after a failed public placement/resource disposal attempt;
- resource cleanup continuing through later child/resource cleanup operations after an individual child-delete failure;
- cleanup failures surfaced rather than silently discarded.

C116 does not yet make lifecycle invalidation or session-wide teardown authoritative for persistent raster state. Those behaviors remain C117.

## Wire contract

The reviewed cleanup vectors are:

```text
placement delete  Ga=d,d=i,i=<image id>,p=<placement id>,q=2
resource delete   Ga=d,d=I,i=<image id>,q=2
```

Placement update reuses the existing reviewed placement command:

```text
Ga=p,i=<image id>,p=<placement id>,C=1[,c=<columns>][,r=<rows>]
```

## TDD evidence

The final valid RED checkpoint was commit `26c9de4f069d9e59b3835513c2df07fa8db969f7`. After correcting test-harness-only syntax and fixture imports, runtime compilation failed across `net8.0`, `net9.0`, and `net10.0` solely because `TerminalRasterPlacement.UpdateAsync(...)` did not yet exist. The RED build reported zero warnings.

The GREEN implementation was committed atomically at `178e19b76ae177119f2da7fcb3c660ddee06b30e`.

That head generated identical public API snapshots across all three target frameworks with fingerprint:

```text
9336a1f6def1c4b02e86db813bae27f45b95af33f47a2cf10dccd4d1d44324f2
```

The intentional C116 baseline freeze was committed at `db5dab910f818bb1894c906c3915ad30dd608037`.

## Qualification

Exact head `db5dab910f818bb1894c906c3915ad30dd608037` passed pull-request workflow `#1571 / 34667244384` across:

- Runtime Windows;
- Runtime Linux;
- Runtime macOS;
- Package candidate / public API freeze;
- Package Foundation;
- Package Presentation;
- Package Semantic and hardening;
- Package Stable 1.x release line;
- Validated package artifact.

C116 is therefore closed. C117 may integrate generation invalidation, managed suspend/resume uncertainty, stale-handle local-only disposal, and session-wide child-before-resource teardown without expanding the C116 public API surface.
