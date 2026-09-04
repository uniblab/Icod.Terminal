# T35 — Public API and Documentation Audit

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Development version:** `0.4.0-alpha.7`  
**Tranche:** T35 — Public API and documentation audit  
**Status:** Complete

---

## 1. Purpose

T35 reviews the complete 0.4 OSC title-operation surface after T29 through T34
have established protocol semantics, validation, framing, endpoint policy, and
session-owned output ordering.

The tranche asks whether the current public API can be carried into stable
`0.4.0` without avoidable regret, whether its documentation states what the
library actually guarantees, and whether later OSC 7/8/52 work has been kept
out of the 0.4 contract.

---

## 2. Reviewed public delta

The 0.4 title API consists of exactly these semantic additions on
`TerminalSession`:

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

The semantic mapping remains:

```text
SetTitleAsync       -> OSC 0 -> icon name and window title
SetIconNameAsync    -> OSC 1 -> icon name only
SetWindowTitleAsync -> OSC 2 -> window title only
```

No rename or signature correction is required before stable 0.4.

---

## 3. Accepted API decisions

T35 accepts the following design choices:

- title operations live on `TerminalSession`, the owner of the live terminal
  conversation;
- the API expresses terminal intent rather than numeric OSC selectors;
- OSC 0, OSC 1, and OSC 2 remain distinguishable;
- no public `SendOsc(...)`, `WriteEscape(...)`, raw selector, or arbitrary
  terminal-control injection surface is introduced;
- no public title-state model is introduced;
- no title stack, title query, or exact prior-title restoration contract is
  promised;
- no support claim is inferred solely from `TERM`, terminfo, or successful byte
  transmission;
- known redirected/non-terminal output is rejected for semantic title
  operations;
- callers do not choose OSC terminator form, C1 form, or payload resource limit;
- validation and wire construction remain internal;
- later OSC 7/8/52 concerns are not encoded speculatively into the 0.4 API.

---

## 4. Safety and resource documentation

The public documentation now records the shared title payload policy:

- canonical outbound framing uses 7-bit `ESC ]` and ST (`ESC \\`);
- title payloads are strict UTF-8;
- C0, DEL, and C1 controls are rejected;
- malformed UTF-16 is rejected;
- payload size is limited to 4096 encoded UTF-8 bytes;
- validation completes before any frame bytes are emitted.

The documentation explicitly describes the 4096-byte bound as an
`Icod.Terminal` safety/resource choice rather than a protocol-standard limit.

---

## 5. Emission and support semantics

T35 freezes the public explanation that title operations are
**emission-oriented**.

Successful completion means the complete frame was handed successfully to the
session output service. It does not mean:

- the emulator supports the selector;
- the emulator applied the title;
- the library observed the resulting title;
- the library can restore an unknown prior title.

The 0.4 surface therefore avoids a misleading public `Supported` or
`Applied` result type.

---

## 6. Ordering and lifecycle documentation

T34 established the concrete ordering contract reviewed here:

- `WriteTextAsync(...)` and the three semantic title methods participate in
  session-owned output serialization;
- they wait behind active session-owned query, presentation, and input-protocol
  output transactions;
- title frames do not implicitly flush;
- once `DisposeAsync()` begins, new high-level application/title writes are
  rejected;
- an operation which already owns output serialization may finish before cleanup
  proceeds.

The borrowed `session.Output` object remains caller-owned and direct writes to it
remain outside session synchronization.

The pre-existing low-level `WriteTerminalStringAsync(...)` and
`WriteCapabilityAsync(...)` methods retain their advanced terminfo-oriented role.
They are not redefined as generic OSC mechanisms during 0.4.

This boundary is now explicit in both the README and the 0.4 public API baseline.

---

## 7. Public API baseline

T35 publishes:

`docs/Public-API-Baseline-0.4.md`

That document extends the previous 0.3 baseline and records the intentional
0.4 public surface, behavioral guarantees, rejected API shapes, ordering rules,
and stable-release conclusion.

---

## 8. README review

The repository README now:

- identifies `0.3.0` as the current stable line and `0.4.0` as active
  development;
- documents the OSC 0/1/2 semantic methods;
- explains the exact method-to-selector mapping;
- records the strict UTF-8/control rejection/4096-byte safety contract;
- explains support uncertainty and emission semantics;
- states that title state is terminal-owned rather than modeled by
  `Icod.Terminal`;
- documents the T34 output-ordering boundary;
- points to the 0.4 roadmap and 0.4 public API baseline.

No documentation currently promises functionality deferred to 0.5 or later.

---

## 9. Explicit non-additions

T35 confirms that stable 0.4 SHALL NOT add solely for API completeness:

- OSC 7 current-directory publication;
- OSC 8 hyperlink APIs;
- OSC 52 clipboard APIs;
- arbitrary OSC selectors;
- BEL terminator selection;
- 8-bit C1 OSC/ST emission;
- title stacks;
- title queries;
- window-management APIs;
- cursor-style APIs;
- synchronized-output APIs.

Those remain later roadmap work.

---

## 10. Completion gate

T35 is complete because:

1. the OSC 0/1/2 semantic method names and signatures have been reviewed;
2. no breaking correction is required before stable 0.4;
3. the public contract does not expose raw OSC selector/framing primitives;
4. support uncertainty and emission semantics are documented honestly;
5. payload safety and resource limits are documented;
6. session output ordering and disposal behavior are documented;
7. title restoration is not promised without observed prior state;
8. the 0.4 public API baseline is published;
9. README documentation reflects the active 0.4 contract;
10. later protocol milestones remain uncommitted by the 0.4 public API.

T36 may now concentrate on package-only consumer validation, cross-host release
gates, release notes, and stable `0.4.0` closure.
