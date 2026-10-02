# Icod.Terminal 1.24 Raster Tile-Atlas Measurement

This witness measures the terminal-protocol work needed by a higher-level retained tile renderer without implementing that renderer in `Icod.Terminal`.

The package-only scripted scenario owns two opaque known frames for one 16-by-16 RGB24 resource. For each workload it applies one-pixel RGB24 regions only to the unselected frame, awaits every acknowledgement, stops on the first failure, selects the frame after all updates succeed, and then swaps its front/back references. The one selection is included in the operation count. The scenario does not call the sequence atomic or gapless.

| Regions | Operations | Updated pixels | Encoded bytes | First acknowledgement | Total | CPU | Allocations |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 2 | 1 | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness |
| 4 | 5 | 4 | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness |
| 16 | 17 | 16 | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness |
| 64 | 65 | 64 | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness | Reported by the package witness |

`packaging/VerifyPersistentRasterPackage.ps1` runs the scenario from the packed artifact on `net8.0`, `net9.0`, and `net10.0`. Each invariant-formatted output row records `regions`, `operations`, `updatedPixels`, total `encodedBytes` observed by the scripted output harness, first-acknowledgement microseconds, total milliseconds, process CPU milliseconds, and allocated bytes. The final qualification run is recorded in the 1.24 development roadmap; its log is the authoritative measurement artifact because timing and allocation values are runner- and framework-dependent.

The scripted terminal returns valid acknowledgements immediately. Therefore these measurements establish bounded encoding, correlation, acknowledgement, and caller sequencing. They do not prove physical rendering, terminal-side storage capacity, visual atomicity, frame-gap behavior, or performance on a particular terminal emulator.

Boundary ownership remains unchanged:

- The application or `Icod.DCurses` owns tile identities, assets, viewport, clipping, damage, overlay order, and text-fallback policy.
- `Icod.Terminal` owns terminal queries, local planning ceilings/counts, intrinsic resource geometry, opaque placeholder/frame identities, acknowledged frame mutations, selection, lifecycle certainty, and cleanup.
- The game owns maps, actors, collision, visibility, time, persistence, and rules.

The 1/4/16/64 trend is the evidence gate for considering a later batching or Indexed8 regional-transfer contract. Version 1.24 does not add either contract.
