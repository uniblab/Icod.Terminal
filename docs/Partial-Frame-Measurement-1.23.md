# Icod.Terminal 1.23 Partial-Frame Measurement

## Purpose

This gate compares the 1.23 bounded region operation with sending the complete frame through the same public `UpdateFrameRegionAsync` path. It measures the package that a consumer restores, including public validation, encoding, APC framing, serialized output, acknowledgement correlation, and semantic result handling.

The benchmark is informational for machine-dependent timings. The package gate fails only if the smaller region does not produce fewer observed wire bytes or if either acknowledged operation fails.

## Reproducible setup

- Harness: `tools/package-persistent-raster-smoke/PersistentRasterCompositionScenario.cs`
- Package: the exact Staging package candidate built by the pull-request workflow
- Runtimes: .NET 8, .NET 9, and .NET 10
- CI environment: GitHub-hosted Ubuntu runner
- Destination: one current opaque animation-frame token in a 16 x 16 RGBA32 resource
- Full transfer: 16 x 16 RGBA32, 1,024 source bytes
- Partial transfer: 4 x 4 RGBA32, 64 source bytes
- Sample size: one warmup for each size, then 32 acknowledged operations for each measurement
- Coordinates: `(0, 0)` for both sizes

The scripted terminal records every emitted APC frame and returns the same correlated positive acknowledgement for both cases. It performs no physical-terminal rendering, network I/O, or artificial delay.

## Recorded metrics

Each target framework prints one line to the package-verification log containing:

- actual framed wire bytes per operation;
- completion latency of the first measured operation;
- total elapsed time for 32 operations;
- process CPU time across those operations; and
- process-wide allocated bytes across those operations.

The exact single-chunk wire sizes are deterministic for this fixture: 1,421 bytes for the complete 16 x 16 frame and 139 bytes for the 4 x 4 region. The bounded update therefore removes 1,282 bytes per operation, a 90.2% wire reduction, while updating 6.25% of the pixels. Exact-wire unit fixtures independently verify the control fields and caller-supplied pixels.

CPU, allocation, and latency values are observations from the named workflow run. Hosted-runner scheduling and runtime JIT/GC behavior make them unsuitable as fixed regression thresholds. Their purpose is to reveal a gross implementation reversal while the deterministic wire result establishes the release value: callers with bounded damage can avoid encoding and transferring unchanged pixels.

## Alpha qualification results

The exact alpha implementation head `cb0a2a9fdd5c43dafbee7ccaf62d46e089fa5a9b` passed all nine jobs in [workflow run 36991995045](https://github.com/uniblab/Icod.Terminal/actions/runs/36991995045). Its fresh-package semantic shard recorded:

| Runtime | Full first | Partial first | Full total | Partial total | Full CPU | Partial CPU | Full allocation | Partial allocation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 8.0.31 | 1,463.2 us | 180.4 us | 2.744 ms | 0.986 ms | 0.000 ms | 10.000 ms | 425,272 B | 255,568 B |
| .NET 9.0.20 | 1,895.1 us | 134.9 us | 3.069 ms | 0.863 ms | 3.170 ms | 0.865 ms | 417,240 B | 252,128 B |
| .NET 10.0.12 | 1,470.4 us | 168.6 us | 3.626 ms | 1.886 ms | 6.707 ms | 3.534 ms | 462,232 B | 294,808 B |

Both cases measured 32 acknowledged operations after warmup. The .NET 8 CPU counter resolution exceeded the sub-millisecond partial workload and produced the expected coarse/noisy inversion; no conclusion is drawn from that counter. Every runtime showed the deterministic 90.2% wire reduction, lower first completion latency, lower total elapsed time, and fewer allocated bytes for the bounded region.

## Interpretation

Partial transfer is retained for 1.23 because it provides a measured, substantial reduction when the caller already knows the damaged rectangle. The operation remains explicit. Terminal does not compute damage, retain source pixels, cache deltas, or claim that a small update is preferable when most of the frame changed.

The benchmark proves the in-memory package path and protocol bytes. It does not claim physical-terminal paint latency or renderer behavior.
