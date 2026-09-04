# Icod.Terminal 0.4 Public API Baseline

**Baseline line:** `0.4.x`  
**Reviewed at:** `0.4.0-alpha.6`  
**Predecessor:** [`Public-API-Baseline-0.3.md`](Public-API-Baseline-0.3.md)  
**Purpose:** intentional source-level inventory and regret review of the public
0.4 OSC title-operation delta

This document extends the 0.3 baseline. Unchanged 0.1/0.2/0.3 signatures remain
part of the consumer contract and are not duplicated here.

The T35 review finds no public 0.4 title member that requires removal, renaming,
or signature change before stable `0.4.0`.

---

## 1. Review outcome

The 0.4 public API remains deliberately semantic rather than exposing arbitrary
terminal control strings or selector numbers.

Accepted design choices:

- OSC title operations are methods on `TerminalSession`, which already owns the
  live terminal conversation;
- OSC 0, OSC 1, and OSC 2 remain separately expressible;
- callers do not provide numeric OSC selectors;
- no public `SendOsc(...)`, `WriteEscape(...)`, or generic protocol-registration
  API is introduced;
- successful completion means the complete frame was emitted to the borrowed
  output service; it does not prove that the terminal applied the requested
  title;
- the library does not claim to own or remember terminal-emulator title state;
- no title-stack, title-query, or exact title-restoration contract is introduced;
- `TERM` and terminfo data are not treated as proof that OSC title operations
  are supported;
- a session which already knows its output endpoint is not a terminal rejects
  the semantic title operation rather than writing OSC bytes into redirected
  output;
- title payload validation, framing, selectors, and resource bounds remain
  internal implementation policy.

No public API correction is required before stable 0.4.

---

## 2. Public title-operation delta

`TerminalSession` adds:

```csharp
public ValueTask SetTitleAsync(
    string value,
    CancellationToken cancellationToken = default
);

public ValueTask SetIconNameAsync(
    string value,
    CancellationToken cancellationToken = default
);

public ValueTask SetWindowTitleAsync(
    string value,
    CancellationToken cancellationToken = default
);
```

The semantic mapping is fixed:

```text
SetTitleAsync       -> OSC 0 -> icon name and window title
SetIconNameAsync    -> OSC 1 -> icon name only
SetWindowTitleAsync -> OSC 2 -> window title only
```

The names describe intent rather than exposing protocol selector numbers.

---

## 3. Wire and payload contract

All three methods share the T29/T30 internal writer contract:

```text
OSC introducer: ESC ]
separator:      ;
terminator:     ESC \\   (ST)
encoding:       strict UTF-8
payload limit:  4096 encoded UTF-8 bytes
```

The 0.4 writer rejects:

- U+0000..U+001F;
- U+007F;
- U+0080..U+009F;
- malformed UTF-16 such as unpaired surrogates;
- a payload whose strict UTF-8 representation exceeds 4096 bytes.

Validation completes before any frame bytes are written. Invalid title text
therefore cannot emit a partial OSC introducer or inject a second terminal
sequence through the semantic title API.

The 4096-byte ceiling is an `Icod.Terminal` resource policy, not a claim that
ECMA-48 or xterm defines that maximum.

---

## 4. Endpoint and support semantics

A semantic title operation requires a session output endpoint which the session
observed as a terminal.

If `OutputObservation.IsTerminal` is false, the title methods fail with
`InvalidOperationException` without writing OSC bytes.

If the endpoint is a terminal but OSC title support is otherwise unknown, 0.4
permits emission. This is deliberately an emission contract:

- successful output does not prove emulator support;
- lack of a dedicated terminfo capability does not prove lack of support;
- a terminal name does not prove support;
- no public "supported" result is fabricated from static identity alone.

A future release may add stronger negotiated support evidence if a concrete
protocol provides it.

---

## 5. Cancellation and output failure

All three methods share these caller-visible rules:

- a null managed string is rejected by normal argument validation;
- caller cancellation observed before transmission emits nothing and surfaces
  through normal `OperationCanceledException` semantics;
- validation errors occur before output;
- once complete-frame transmission begins, the internal OSC writer does not
  deliberately abandon that frame in response to ordinary caller cancellation;
- an output exception remains an output exception and is not converted into a
  protocol-support result.

No response transaction or input-side expectation is created for OSC 0/1/2 in
0.4.

---

## 6. Session output ordering

T34 integrates the semantic title methods with the existing session-owned
control-output semaphore.

The following public high-level operations participate in coordinated session
ordering:

- `WriteTextAsync(...)`;
- `SetTitleAsync(...)`;
- `SetIconNameAsync(...)`;
- `SetWindowTitleAsync(...)`.

They therefore do not interleave with a session-owned query request,
presentation transition, or rich-input protocol transition while that subsystem
owns the control-output lease.

Title operations do not implicitly flush. Query/state-transition paths retain
their existing explicit flush behavior, and session disposal performs final
flush/restoration.

Once `DisposeAsync()` begins, new high-level application/title output is
rejected. An operation which already acquired session output ownership is
allowed to finish before cleanup obtains the same serialization boundary.

---

## 7. Low-level borrowed-output boundary

`TerminalSession.Output` remains the caller-owned `ITerminalOutput` service that
was supplied when the session opened. Direct calls to that borrowed object are
outside the session's ordering contract and remain caller-synchronized.

Likewise, the pre-existing low-level terminal-string/capability APIs remain an
advanced protocol surface used by the session's internal transition managers:

```csharp
public ValueTask WriteTerminalStringAsync(
    string value,
    int affectedLines = 1,
    CancellationToken cancellationToken = default
);

public ValueTask<bool> WriteCapabilityAsync(
    StringCapability capability,
    int affectedLines = 1,
    CancellationToken cancellationToken = default
);
```

These APIs predate the 0.4 title feature and are not widened into a public OSC
escape hatch. Consumers which require ordinary coordinated application output
should prefer `WriteTextAsync(...)`; consumers should prefer the semantic title
methods over synthesizing OSC title frames manually.

This distinction is intentional for 0.4 and avoids introducing re-entrant
output-lock behavior into the internal terminfo transition paths during the
OSC-title release.

---

## 8. Explicitly rejected public shapes

The T35 review rejects introducing these public forms in 0.4:

```text
SendOsc(int selector, string payload)
SendOsc(string selector, string payload)
WriteEscape(string sequence)
SetOscTitle(int selector, string value)
PushTitle(...)
PopTitle(...)
GetTitleAsync(...)
```

No public terminator selection, payload-size override, or 8-bit C1 emission
option is exposed.

OSC 7, OSC 8, OSC 52, cursor-style operations, and synchronized output remain
later milestone concerns and do not shape the 0.4 title API speculatively.

---

## 9. Stable 0.4 conclusion

The reviewed 0.4 title-operation delta is suitable to carry forward to stable
`0.4.0` without a breaking API correction.

T36 should concentrate on package-only consumer validation, release metadata,
public documentation closure, and stable release gates. It should not redesign
the title API unless validation discovers a release-blocking defect.
