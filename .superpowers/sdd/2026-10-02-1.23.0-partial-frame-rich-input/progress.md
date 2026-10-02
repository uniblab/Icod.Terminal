# 1.23.0 implementation ledger

## Shared interface preflight

- Task 1 freezes RED fixtures consumed by Tasks 2-6.
- Task 2 validates destination/frame/region and passes immutable direct raster data to Task 3.
- Task 3 emits the private `a=f` frame edit consumed by Task 4's acknowledgement path.
- Task 5 inventories existing semantic input coverage before Task 6 changes behavior.
- Tasks 4 and 6 supply the public contracts used by samples, package witnesses, and qualification.

## Progress

- [x] Read approved design and implementation plan.
- [x] Confirm official Kitty partial-frame edit semantics.
- [x] Add initial RED partial-frame contract fixtures.
- [x] Observe intended RED failure in CI (run 36989498423: missing `UpdateFrameRegionAsync` on net8/net9/net10).
- [x] Implement partial-frame API and transaction; alpha CI green.
- [x] Complete rich-input inventory and implement legacy Menu plus empty-buffer malformed-frame recovery; alpha CI green.
- [x] Update samples, docs, package witnesses, and qualification evidence; alpha CI green.
- [x] Add package-only wire/CPU/allocation/latency measurement and multi-chunk encoder coverage.
- [x] Pass all nine jobs on reviewed alpha head `cb0a2a9fdd5c43dafbee7ccaf62d46e089fa5a9b` in run 36991995045.
- [ ] Pass all nine jobs on the stable 1.23.0 head and record artifact evidence.

## Rulings

- 2026-10-02: Use Kitty `a=f` with `r`, `x`, `y`, `s`, `v`, and `X=1`, as specified by the official protocol. Chunk continuations retain `a=f`.
- 2026-10-02: The verified decoder gap is legacy Kitty Menu (`CSI 29 ~`) with event phases. The obsolete F3 `CSI R` form remains excluded because the current protocol removed it to avoid collision with cursor-position reports.
