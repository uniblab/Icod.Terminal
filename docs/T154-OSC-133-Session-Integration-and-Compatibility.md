# T154 — OSC 133 Session Integration and Compatibility

**Release:** `Icod.Terminal 0.15.0`  
**Development version:** `0.15.0-alpha.5`  
**Status:** Implemented; exact-head validation pending

## Purpose

T154 adds no new protocol or public API. It proves that the T152/T153 typed OSC 133 metadata APIs remain ordinary `TerminalSession` output operations and compose with the established output/query architecture.

## Compatibility invariants

The portable 0.12 OSC 133 surface remains byte-for-byte unchanged:

```text
OSC 133;A ST
OSC 133;B ST
OSC 133;C ST
OSC 133;D;status ST
OSC 133;D ST
```

Extended metadata continues to use the same session output serialization domain as existing terminal output. T154 introduces no second writer, response reader, query router, background listener, support cache, or lifecycle manager.

## Coverage added

`TerminalSemanticPromptCompositionTests` now retains the original T126 bare-marker composition tests and adds extended-metadata cases proving:

1. typed prompt and command-output metadata compose in deterministic order with ordinary application text, OSC 7 current-location publication, OSC 8 hyperlinks, OSC 52 clipboard output, OSC 22 pointer shape, and portable command completion;
2. extended OSC 133 metadata composes while DEC private mode 2026 synchronized output and OSC 9;4 progress ownership are active, with their cleanup frames and flush semantics unchanged;
3. extended OSC 133 metadata can be emitted while a `QueryDeviceStatusAsync(...)` transaction is outstanding, and the existing single query/response router still correlates `CSI 0 n` correctly.

These tests validate emitted bytes directly and do not depend on the CI host terminal recognizing OSC 133 extended metadata.

## Architectural conclusion

No production changes were required for T154. The existing session output gate and active-query router already provide the required composition guarantees. Adding another manager or protocol path would duplicate established synchronization and weaken the architecture.

## Gate

T154 is complete when the exact `0.15.0-alpha.5` PR head is green on Windows, Linux, and macOS, including all retained package/downstream gates.
