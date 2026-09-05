# T91 — Synchronized-Output CSI Primitive

**Release:** `0.9.0`  
**Tranche:** `T91`  
**Development version:** `0.9.0-alpha.2`  
**Status:** Implemented; validation pending  
**Theme:** canonical mode-2026 begin/end framing with complete-frame commit semantics

## 1. Scope

T91 implements only the internal wire primitive required by later synchronized-output ownership work.

It does not add a state manager, public lease, capability detection, lifecycle behavior, or output transaction policy.

## 2. Canonical frames

The internal writer now exposes specialized synchronized-output helpers for:

```text
begin -> ESC [ ? 2 0 2 6 h
end   -> ESC [ ? 2 0 2 6 l
```

The helpers delegate to the existing structural `CsiWriter.EncodeFrame(...)` implementation rather than introducing a generic private-mode abstraction.

## 3. Internal API

The T91 additions are:

```csharp
internal static byte[] EncodeSynchronizedOutputBeginFrame();
internal static byte[] EncodeSynchronizedOutputEndFrame();

internal static ValueTask WriteSynchronizedOutputBeginAsync(
    ITerminalOutput output,
    CancellationToken cancellationToken = default
);

internal static ValueTask WriteSynchronizedOutputEndAsync(
    ITerminalOutput output,
    CancellationToken cancellationToken = default
);
```

No public API is added.

## 4. Commit and cancellation semantics

The specialized writers preserve the established complete-frame policy:

- validate the output service before use;
- observe caller cancellation before commit;
- construct the complete CSI frame before transmission;
- observe cancellation once more before transmission commits;
- perform the underlying transport write with `CancellationToken.None` so caller cancellation cannot truncate a committed control frame.

Neither helper flushes the output service.

Flush policy belongs to the synchronized-output manager in T92 because begin/end framing and visibility-boundary policy are separate responsibilities.

## 5. Tests

T91 extends `CsiWriterTests` with:

- byte-exact begin encoding;
- byte-exact end encoding;
- one transport write per begin operation;
- one transport write per end operation;
- non-cancellable transport token after commit;
- no implicit flush;
- pre-commit cancellation emits nothing for begin;
- pre-commit cancellation emits nothing for end.

## 6. Architectural boundary

T91 deliberately does not add:

- public `DECSET`/`DECRST` APIs;
- caller-supplied private-mode numbers;
- a generic private-mode writer;
- a synchronized-output manager;
- nesting/ref-count semantics;
- final-release flush behavior;
- support probing.

Those belong to T92 and later tranches.

## 7. Acceptance gate

T91 is complete when the new primitive is green across the repository's supported target frameworks and existing CSI/DECSCUSR behavior remains unchanged.
