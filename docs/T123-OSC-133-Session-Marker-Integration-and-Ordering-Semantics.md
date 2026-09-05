# T123 — OSC 133 Session Marker Integration and Ordering Semantics

**Release:** `0.12.0`  
**Tranche:** `T123`  
**Development version:** `0.12.0-alpha.4`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T123 integrates the T122 semantic OSC 133 marker model with `TerminalSession` so prompt/command annotations participate in the same ordered output domain as application text and other session-owned terminal control output.

This tranche deliberately does **not** add the public OSC 133 methods assigned to T124.

---

## 2. Reconciliation with the T120 freeze

The pre-freeze roadmap described T123 as a possible command-region state model with legal/illegal prompt/input/execution/completion transitions.

T120 subsequently froze a different and stronger interoperability posture: OSC 133 marker methods are independently callable and `Icod.Terminal` does not maintain a synthetic shell-history state machine.

T123 therefore implements the frozen T120 contract rather than the earlier placeholder wording.

There is no retained in-memory state equivalent to:

```text
Prompt -> Input -> Output -> Finished
```

and no marker is rejected merely because another marker was not previously observed through the same `TerminalSession`.

---

## 3. Session integration

T123 adds one internal session operation which accepts the typed T122 marker value.

For every marker it:

1. validates the semantic marker before output work;
2. observes caller cancellation;
3. requires an interactive output endpoint;
4. acquires the existing session output serialization gate;
5. observes cancellation again immediately before marker emission;
6. delegates to the T122/T121 semantic writer;
7. commits the complete OSC frame using a non-cancellable transport write;
8. releases the session output gate immediately after that individual marker write;
9. performs no implicit flush.

No second output transport or OSC-specific serialization mechanism is introduced.

---

## 4. Ordering semantics

Each OSC 133 marker is serialized through the same `TerminalSession` output gate used by application text and other terminal-control operations.

This provides deterministic byte ordering such as:

```text
marker -> text
text -> marker
control output -> marker
marker -> control output
```

without holding the gate across an entire command region.

Canonical shell integration remains:

```text
A -> B -> C -> D
```

but that sequence is documentation for callers, not an enforced session state machine.

Out-of-order or partial sequences remain emit-capable because shells may redraw prompts, attach integration late, recover after interruption, or compose nested/subshell behavior outside the knowledge of one library instance.

---

## 5. Failure semantics

A marker write does not mutate retained semantic command-region state because no such state exists.

If a committed transport write fails:

- the failure propagates to the caller;
- no compensating `D`, abort, prompt-start, or other marker is fabricated;
- `TerminalSession` does not claim that terminal command history is known;
- later markers remain independently callable;
- ordinary terminal mode/presentation state is not invalidated merely because an OSC 133 annotation write failed.

This is the truthful consequence of the T120 event-annotation model.

T125 may add further failure/lifecycle proofs, but it must not reintroduce synthetic command-region state.

---

## 6. Cancellation semantics

Caller cancellation is honored while waiting for the shared session output gate and again before commit.

If cancellation wins before commit, no OSC 133 bytes are emitted.

Once the specialized T121 writer commits the complete frame, the underlying write uses `CancellationToken.None`, preserving the established complete-frame transaction rule.

---

## 7. Interactive-output requirement

OSC 133 semantic annotations require an output endpoint observed as an interactive terminal.

A session opened with `RequireInteractiveOutput = false` may still exist over redirected output for other purposes, but T123 rejects OSC 133 marker emission on that non-terminal output endpoint.

Successful emission remains optimistic: it proves that the frame was written, not that the terminal implements OSC 133.

---

## 8. Lifecycle posture

T123 adds no lifecycle participant and no OSC 133 ownership manager.

Suspend, resume, invalidation, and session disposal do not automatically emit semantic markers.

Disposal uses the existing session-output closure rule: after the session stops accepting application/session output, a marker call is rejected with `ObjectDisposedException` rather than emitting anything.

---

## 9. Tests

`TerminalSessionSemanticPromptTests` proves:

- a marker waits for an in-progress application-text write;
- application text waits for an in-progress marker write;
- a marker waits behind the shared control-output gate;
- marker writes never overlap another session-owned write;
- noncanonical marker ordering remains legal and is emitted exactly as requested;
- committed marker writes use a non-cancellable transport token;
- marker operations do not flush implicitly;
- cancellation while waiting for output emits nothing;
- a failed marker write produces no compensating marker and does not block a later independent marker;
- marker failure does not invalidate unrelated session terminal state;
- noninteractive output is rejected;
- disposed sessions reject marker emission.

---

## 10. T123 decision

OSC 133 semantic markers now participate in deterministic `TerminalSession` output serialization without creating command-history state, long-lived ownership, or lifecycle behavior.

T124 may expose the five frozen public semantic operations on top of this session integration:

```text
BeginPrompt
BeginCommandInput
BeginCommandOutput
FinishCommand(exitStatus)
AbortCommand
```

while preserving the independent-call semantics frozen by T120.
