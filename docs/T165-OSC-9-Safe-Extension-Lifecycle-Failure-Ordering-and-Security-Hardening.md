# T165 — OSC 9 Safe Extension Lifecycle, Failure, Ordering, and Security Hardening

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T165  
**Version:** `0.16.0-alpha.6`  
**Status:** Implemented; exact-head validation pending

## Purpose

T165 adds no new protocol or public API. It hardens the T162 notification and T163 OSC 9;9 compatibility operations against cancellation, transport failure, concurrency, lifecycle transitions, disposal, and accidental public exposure of excluded hazardous vendor commands.

## Coverage

`TerminalSessionOsc9SafeExtensionHardeningTests` proves:

1. a notification cancelled while waiting for the session output gate emits no bytes;
2. a committed notification transport failure propagates without compensating output, does not invalidate the session, and does not prevent a later independent OSC 9;9 call;
3. notification output and retained OSC 9;4 progress serialize as whole frames through the same session output domain, with committed writes non-cancellable;
4. `InvalidateState()` does not replay, restore, or synthesize notification/OSC 9;9 traffic;
5. suspend/resume does not replay either safe OSC 9 operation, while explicit post-resume notification remains available;
6. disposal and repeated disposal do not synthesize or replay OSC 9 metadata;
7. the public `TerminalSession` surface contains the two intended safe methods but no generic/raw OSC 9 method and no public method names corresponding to sleep, modal message boxes, wait-for-key, GUI macro execution, process launch, environment disclosure, or xterm-emulation mutation.

Existing T161–T163 tests remain authoritative for malformed UTF-16, C0/DEL/C1 rejection, injection safety, exact payload boundaries, oversize rejection, empty notification semantics, empty-path rejection, and noninteractive endpoint rejection.

## Architectural conclusion

No production changes were required. The existing session output serialization/lifecycle architecture already supplies the required failure and ordering behavior.

The safety boundary remains semantic rather than generic: callers can request only notification or explicit Windows-current-directory compatibility. The library does not provide an arbitrary OSC 9 command-number or raw payload escape hatch.

## Gate

T165 is complete when the exact `0.16.0-alpha.6` PR head is green on Windows, Linux, and macOS with all retained package/downstream gates.

Next: T166 — real downstream `Icod.DCurses` acceptance.
