# T155 — OSC 133 Lifecycle, Failure, Ordering, and Security Hardening

**Release:** `Icod.Terminal 0.15.0`  
**Version:** `0.15.0-alpha.6`  
**Status:** Implemented; exact-head validation pending

## Purpose

T155 hardens the typed extended OSC 133 surface added by T152/T153 without adding protocol forms or public API.

The tranche verifies that extended prompt/command-output metadata obeys the same lifecycle, output-commit, failure, and serialization rules already proven for the portable OSC 133 core.

## Added coverage

`TerminalSessionExtendedSemanticMetadataHardeningTests` proves:

- a cancelled extended prompt waiting for the session output lease emits no bytes;
- a committed extended command-output write failure propagates without compensating OSC traffic and does not poison later semantic-marker calls;
- concurrent extended prompt/command-output calls serialize as complete frames rather than interleaving bytes;
- committed frame writes are non-cancellable;
- `InvalidateState()` does not replay, restore, or synthesize extended semantic metadata;
- managed suspend/resume emits no extended-marker replay;
- after resume, ordinary explicit marker calls remain available;
- session disposal and repeated disposal do not synthesize `D`, abort, or replay metadata for an unfinished extended command-output region.

T151/T153 coverage remains authoritative for:

- malformed UTF-16 rejection;
- injection-safe percent encoding;
- exact 65,536-byte encoded payload acceptance;
- one-byte-over rejection;
- pre-cancelled command-output calls;
- protocol/control-byte escaping.

## Production-code result

No production change was required.

The extended OSC 133 APIs already:

- acquire the existing `TerminalSession` output serialization lease;
- fully encode/validate metadata before committed transport output;
- perform one non-cancellable frame write after commitment;
- retain no logical lifecycle state requiring restoration;
- create no background listener, query, support cache, or alternate output path.

T155 therefore closes as a hardening/test tranche rather than an architecture change.

## Invariants retained

- no session-open OSC 133 auto-emission;
- no resume replay;
- no invalidation traffic;
- no synthesized completion/abort during disposal;
- no generic raw OSC 133 parameters;
- no `%q` command-line path;
- no automatic command capture/redaction;
- one session output serialization domain;
- single active-query/input router remains unchanged.

## Next gate

The exact `0.15.0-alpha.6` PR head must pass the full Windows/Linux/macOS Staging workflow before T156 downstream acceptance begins.
