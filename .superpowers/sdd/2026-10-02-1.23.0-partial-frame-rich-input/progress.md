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
- [ ] Observe intended RED failure in CI.
- [ ] Implement partial-frame API and transaction.
- [ ] Complete rich-input inventory and missing cases.
- [ ] Update samples, docs, package witnesses, and qualification evidence.

## Rulings

- 2026-10-02: Use Kitty `a=f` with `r`, `x`, `y`, `s`, `v`, and `X=1`, as specified by the official protocol. Chunk continuations retain `a=f`.
