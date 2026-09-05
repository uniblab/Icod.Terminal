# T94 — Synchronized Output Composition and Ordering

**Release:** `0.9.0`  
**Tranche:** `T94`  
**Development version:** `0.9.0-alpha.5`  
**Status:** Implemented; exact-head validation pending  
**Theme:** shared output serialization and composition with existing semantic terminal operations

## 1. Objective

T94 proves that synchronized-output ownership composes with the existing `TerminalSession` output/query surface without creating a second serialization domain or silently changing the semantics of contained operations.

The synchronized-output lease contributes only:

```text
first owner -> CSI ? 2026 h
last owner  -> CSI ? 2026 l + one flush
```

Contained operations retain their existing framing, cancellation, query, and flush behavior.

## 2. Shared output gate

All session-owned protocol output must serialize through the existing `TerminalSession` control-output gate.

During the T94 audit, the terminfo path exposed a pre-existing exception to that rule: `WriteTerminalStringAsync(...)` delegated through `TerminalOutputStream` without first acquiring `AcquireSessionOutputAsync(...)`.

T94 corrects that path so resolved terminfo strings and `WriteCapabilityAsync(...)` now participate in the same session-owned serialization domain as:

- application text;
- synchronized-output begin/end frames;
- OSC title/location/hyperlink/clipboard output;
- cursor-style frames;
- presentation transitions;
- active query requests.

No second gate or synchronized-output-specific writer is introduced.

## 3. Semantic output composition

The acceptance matrix brackets existing operations inside one synchronized-output lease and verifies deterministic byte order:

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

T94 adds no public API.

The public surface remains:

```csharp
public sealed class TerminalSynchronizedOutputLease : IAsyncDisposable

public ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
	CancellationToken cancellationToken = default
);
```

## 7. Acceptance coverage

`TerminalSynchronizedOutputCompositionTests` verifies:

- synchronized-output begin precedes all contained semantic output;
- existing semantic frames remain byte-exact;
- resolved terminfo output is ordered through the shared session gate;
- presentation transitions remain legal inside synchronized output;
- contained flush semantics are preserved;
- final synchronized-output release is the final frame and contributes one flush;
- an active Device Status Report query completes while synchronized output is active.

## 8. T94 decision

Synchronized output is an ownership and presentation-timing bracket around ordinary `TerminalSession` activity. It is not a new byte-buffering subsystem and it does not change the semantics of the operations executed within the scope.

T95 may therefore focus exclusively on lifecycle, cancellation, failure recovery, and disposal stress rather than further output-path architecture changes.
