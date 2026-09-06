# T164 — OSC 9 Safe Extension Composition and Compatibility

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T164  
**Version:** `0.16.0-alpha.5`  
**Status:** Implemented; exact-head validation pending

## Purpose

T164 adds no protocol or public API. It proves the T162 notification and T163 OSC 9;9 compatibility operations remain ordinary `TerminalSession` output operations and compose with retained semantic output, ownership managers, synchronized output, and the active terminal-query router.

## Coverage

`TerminalOsc9SafeExtensionCompositionTests` proves byte-exact ordering for:

1. notification and OSC 9;9 alongside OSC 0/1/2 title publication, preferred OSC 7 current-location publication, portable OSC 133 command-region markers, OSC 8 hyperlinks, OSC 52 clipboard output, OSC 22 pointer shape, indexed palette mutation, and dynamic-color mutation;
2. notification and OSC 9;9 while DEC private mode 2026 synchronized output and retained OSC 9;4 progress ownership are active, including unchanged progress cleanup and synchronized-output flush behavior;
3. notification and OSC 9;9 while `QueryDeviceStatusAsync(...)` is outstanding, proving the existing single active-query input/router path continues to correlate the CSI response independently of advisory OSC 9 output.

## Compatibility conclusions

No production changes were required.

The established `TerminalSession` output serialization gate already provides the required ordering. T164 therefore introduces no new manager, writer domain, response reader, terminal-brand detector, support cache, or lifecycle state.

The tests also make the OSC 7/OSC 9;9 relationship concrete: OSC 7 remains a distinct preferred semantic operation and OSC 9;9 remains an explicit compatibility call. Their frames appear independently and in caller order.

Existing OSC 9;4 progress remains on its original implementation and retains its BEL-terminated wire contract. The new text-bearing OSC 9 operations retain their ST-terminated bounded contract; the families coexist without sharing or changing framing rules.

## Gate

T164 is complete when the exact `0.16.0-alpha.5` PR head is green on Windows, Linux, and macOS with all retained downstream/package gates.

Next: T165 — lifecycle, failure, ordering, and security hardening.
