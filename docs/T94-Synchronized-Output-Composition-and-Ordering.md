# T94 — Synchronized Output Composition and Ordering

**Release:** `0.9.0`  
**Tranche:** `T94`  
**Development version:** `0.9.0-alpha.5`  
**Status:** Implemented; cumulative exact-head validation required  
**Theme:** composition with existing semantic terminal operations without changing established low-level output contracts

## 1. Objective

T94 proves that synchronized-output ownership composes with the existing `TerminalSession` output/query surface without creating a second serialization domain or silently changing the semantics of contained operations.

The synchronized-output lease contributes only:

```text
first owner -> CSI ? 2026 h
last owner  -> CSI ? 2026 l + one flush
```

Contained operations retain their existing framing, cancellation, ordering, and flush behavior.

## 2. Existing output coordination remains authoritative

Most semantic session operations serialize through the existing `TerminalSession` control-output gate. Higher-level transition managers such as presentation and rich-input protocol ownership already acquire that gate around their transition batches.

The pre-existing `WriteTerminalStringAsync(...)` / `WriteCapabilityAsync(...)` surface is different by design. `WriteTerminalStringAsync(...)` is the advanced low-level terminfo primitive used by transition managers while they already own the control-output lease. Its core writer therefore must not recursively acquire the non-reentrant session gate.

T94 initially attempted to move the core terminfo writer under `AcquireSessionOutputAsync(...)`. The regression suite correctly exposed that as a self-deadlock because presentation/input-protocol managers already hold the gate before invoking terminfo output. The change was reverted and the historical contract retained:

- higher-level session managers provide ordered terminfo transition batches while holding the control-output gate;
- direct external callers of the low-level `WriteTerminalStringAsync(...)` primitive remain responsible for coordinating that raw protocol output with other concurrent session traffic;
- synchronized output does not redefine that established responsibility.

No second gate or synchronized-output-specific writer is introduced.

## 3. Semantic output composition

The acceptance matrix brackets sequential existing operations inside one synchronized-output lease and verifies deterministic byte order:

```text
CSI ? 2026 h
application text
resolved terminfo output
OSC 0 title
OSC 7 location
OSC 8 begin
hyperlink text
OSC 8 end
OSC 52 clipboard write
DECSCUSR cursor style
presentation cursor-hide
presentation cursor-restore
CSI ? 2026 l
```

The synchronized-output lease does not rewrite, buffer, aggregate, or otherwise reinterpret these operations.

The raw terminfo call in this acceptance sequence is intentionally sequential. T94 does not create a new concurrent-serialization guarantee for `WriteTerminalStringAsync(...)`.

## 4. Flush composition

Contained operations preserve their existing flush contracts.

In particular, presentation-state transitions may flush according to their established behavior while synchronized output is active. T94 does not suppress those flushes because mode 2026 is a terminal-side presentation-deferral mechanism, not an `Icod.Terminal` transport buffer.

The final synchronized-output release still contributes exactly one additional flush after `CSI ? 2026 l`.

## 5. Active queries

T94 proves that an active CSI query can execute while synchronized-output ownership is active:

```text
CSI ? 2026 h
CSI 5 n          request Device Status Report
CSI 0 n          terminal response arrives on the shared input path
CSI ? 2026 l
flush
```

The query continues to use the existing single-reader transaction manager. Synchronized output introduces no response reader, no query-specific output path, and no hidden timeout adjustment.

If a terminal implementation chooses to delay a response while mode 2026 is active, the normal caller-visible query timeout remains authoritative.

## 6. Public API impact

T94 adds no public API and does not redefine the concurrency contract of existing low-level terminfo output APIs.

The synchronized-output public surface remains:

```csharp
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable

public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
	CancellationToken cancellationToken = default
);
```

## 7. Acceptance coverage

`TerminalSynchronizedOutputCompositionTests` verifies:

- synchronized-output begin precedes sequential contained output;
- existing semantic frames remain byte-exact;
- resolved terminfo output remains usable inside synchronized output;
- presentation transitions remain legal inside synchronized output;
- contained flush semantics are preserved;
- final synchronized-output release is the final frame and contributes one flush;
- an active Device Status Report query completes while synchronized output is active.

The existing presentation/input-protocol regression suite separately proves that manager-owned terminfo transition batches remain non-deadlocking under their established gate ownership.

## 8. T94 decision

Synchronized output is an ownership and presentation-timing bracket around ordinary `TerminalSession` activity. It is not a new byte-buffering subsystem and it does not silently strengthen or weaken the pre-existing concurrency contract of advanced raw terminfo output.

T95 therefore focuses on lifecycle, cancellation, failure recovery, and disposal stress rather than redesigning the established output architecture.
