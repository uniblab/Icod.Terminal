# Icod.Terminal 1.24 Raster Tile-Atlas Measurement

This witness measures the terminal-protocol work needed by a higher-level retained tile renderer without implementing that renderer in `Icod.Terminal`.

The package-only scripted scenario owns two opaque known frames for one 16-by-16 RGB24 resource. For each workload it applies one-pixel RGB24 regions only to the unselected frame, awaits every acknowledgement, stops on the first failure, selects the frame after all updates succeed, and then swaps its front/back references. The one selection is included in the operation count. The scenario does not call the sequence atomic or gapless.

GitHub Actions run `37027131690` at source `878832ac3abb8bba46e076e3b3a6e15f55a97f61` produced these package-only results on the Linux runner:

| Framework | Regions | Operations | Updated pixels | Encoded update bytes | First ack (µs) | Total (ms) | CPU (ms) | Allocations (bytes) |
|:---|---:|---:|---:|---:|---:|---:|---:|---:|
| net8.0 | 1 | 2 | 1 | 55 | 252.0 | 2.688 | 0.000 | 24,504 |
| net8.0 | 4 | 5 | 4 | 220 | 106.7 | 0.476 | 0.000 | 47,032 |
| net8.0 | 16 | 17 | 16 | 886 | 127.5 | 1.532 | 20.000 | 166,080 |
| net8.0 | 64 | 65 | 64 | 3,544 | 85.2 | 5.111 | 10.000 | 612,000 |
| net9.0 | 1 | 2 | 1 | 55 | 309.7 | 2.056 | 3.370 | 18,648 |
| net9.0 | 4 | 5 | 4 | 220 | 108.6 | 0.458 | 1.198 | 42,936 |
| net9.0 | 16 | 17 | 16 | 886 | 90.7 | 1.193 | 3.077 | 151,640 |
| net9.0 | 64 | 65 | 64 | 3,544 | 355.0 | 4.830 | 18.106 | 582,384 |
| net10.0 | 1 | 2 | 1 | 55 | 176.1 | 1.903 | 2.480 | 17,616 |
| net10.0 | 4 | 5 | 4 | 220 | 104.8 | 0.445 | 0.698 | 42,656 |
| net10.0 | 16 | 17 | 16 | 886 | 95.0 | 1.366 | 3.287 | 151,336 |
| net10.0 | 64 | 65 | 64 | 3,544 | 170.0 | 4.969 | 14.029 | 580,224 |

`packaging/VerifyPersistentRasterPackage.ps1` runs the scenario from the packed artifact on `net8.0`, `net9.0`, and `net10.0`. Each invariant-formatted output row records `regions`, `operations`, `updatedPixels`, total regional-update `encodedBytes` observed by the scripted output harness, first-acknowledgement microseconds, total milliseconds, process CPU milliseconds, and allocated bytes. The operation count and total time include the final selection; the encoded-byte subtotal intentionally isolates the update frames that a later batching contract could change. The run log is the authoritative measurement artifact because timing, CPU, and allocation values are runner- and framework-dependent. Encoded update bytes grow from 55 per one-digit-coordinate update; the 16/64-region totals also reflect the longer decimal coordinate fields, so the witness reports totals rather than assuming a constant per-update frame length.

The scripted terminal returns valid acknowledgements immediately. Therefore these measurements establish bounded encoding, correlation, acknowledgement, and caller sequencing. They do not prove physical rendering, terminal-side storage capacity, visual atomicity, frame-gap behavior, or performance on a particular terminal emulator.

Boundary ownership remains unchanged:

- The application or `Icod.DCurses` owns tile identities, assets, viewport, clipping, damage, overlay order, and text-fallback policy.
- `Icod.Terminal` owns terminal queries, local planning ceilings/counts, intrinsic resource geometry, opaque placeholder/frame identities, acknowledged frame mutations, selection, lifecycle certainty, and cleanup.
- The game owns maps, actors, collision, visibility, time, persistence, and rules.

The 1/4/16/64 trend is the evidence gate for considering a later batching or Indexed8 regional-transfer contract. Version 1.24 does not add either contract.
