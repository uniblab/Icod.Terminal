# T175 — Composition and Hardening

**Release:** `Icod.Terminal 0.17.0`  
**Tranche:** T175  
**Version:** `0.17.0-alpha.6`  
**Status:** Complete; implementation hardening green at workflow #831

## Purpose

T175 closes the cross-manager and lifecycle obligations deliberately carried forward from T170/T173. It does not add another keyboard protocol or widen the public keyboard API. Its purpose is to make negotiated Kitty keyboard ownership compose safely with presentation state, lifecycle re-entry, concurrent leases, and transition failures.

## Shared composition serialization

Keyboard reporting is owned by `TerminalInputProtocolManager`, while main/alternate-screen state is owned by `TerminalPresentationManager`. Kitty maintains independent keyboard stacks for the main and alternate screens, so those managers cannot transition independently when an alternate-screen boundary is crossed.

T175 introduces one shared session composition serialization domain for the two managers. External mutation entry points acquire the composition gate before entering either manager's private gate.

The resulting lock order is:

```text
session state-composition gate
    -> input/presentation manager gate
        -> session control-output serialization
```

Input-protocol acquisition, presentation acquisition, managed lifecycle suspend/re-entry/close, and both lease-disposal paths participate in this ordering. The Kitty support probe for a new keyboard lease is also inside the composition boundary, so probe traffic cannot interleave with a managed screen handoff.

## Managed screen choreography

When keyboard reporting is active, a presentation transition that changes the managed screen is one composition operation:

```text
pop current Kitty entry
switch managed screen
push desired Kitty entry on the active screen
```

Entering alternate screen therefore produces:

```text
CSI < u
[enter alternate-screen capability]
CSI > flags u
```

Leaving alternate screen produces the corresponding pop / leave / push sequence.

Bracketed paste, focus reporting, and mouse reporting remain active and are not unnecessarily cycled during the screen handoff.

## Failure and rollback

The existing transactional presentation and input-protocol behavior is retained and extended across the screen/keyboard boundary.

If the physical screen transition fails after the Kitty pop, the presentation transition rolls back and Kitty reporting is re-established on the original active screen.

If the screen transition succeeds but the replacement Kitty push fails, T175 attempts to restore the previous presentation screen and then re-establish keyboard reporting there. Multiple failures are surfaced as an aggregate exception rather than silently advancing believed state.

A failed handoff does not permanently poison later independent transitions when rollback succeeds.

## Concurrency and lock-order resistance

Focused adversarial coverage now holds the physical alternate-screen write in progress while a concurrent keyboard lease release is attempted. The release must remain blocked until the complete pop / switch / push handoff finishes. Only then may it remove the newly active Kitty entry.

This proves the former interleaving window is closed: a concurrent input lease mutation cannot re-push, downgrade, or pop Kitty between the managed-screen pop and switch.

The shared ordering also avoids manager-to-manager nested lock inversion. Presentation code may call the input manager for screen-local Kitty choreography while already owning the outer composition domain, but ordinary input lease mutations cannot simultaneously enter the competing path.

## Lifecycle re-establishment

Managed suspend continues to restore input protocols before process suspension, including popping the current library-owned Kitty stack entry. Managed resume then:

1. restores ordinary session host state;
2. re-enters presentation state;
3. re-detects Kitty support through the internal post-resume observation-query path;
4. pushes the currently desired keyboard reporting mode;
5. resumes the ordinary public query path only after lifecycle re-entry is complete.

An externally observed resume that did not follow the library's managed suspend uses the ordinary active-query path when public query transactions remain live. The resume path chooses the query mechanism from actual query-transaction state rather than assuming every resume followed a managed suspend.

`InvalidateState()` continues to mark believed input/presentation state untrustworthy; the managers do not silently claim known physical state after invalidation.

## Validation

T175 focused tests cover:

- exact Kitty pop / alternate-screen enter / Kitty push;
- exact Kitty pop / alternate-screen exit / Kitty push;
- coexistence with bracketed paste, focus reporting, and mouse reporting without cycling those protocols;
- managed suspend/resume Kitty pop, fresh support probe, and replacement push;
- concurrent keyboard lease release blocked across an in-progress screen handoff;
- exact post-handoff release ordering;
- injected screen-switch output failure;
- successful Kitty restoration after failed screen switch;
- successful retry after rollback;
- retained traditional input, modern decoder, query, lifecycle, presentation, and package tests.

The implementation-hardening head `28921742a09392f5948e825385dd8808d82bb819` passed Windows, Linux, and macOS PR validation at workflow #831. Linux also passed the exact Staging package validation and all retained 0.8–0.16 package contracts.

## Deliberately unchanged scope

T175 adds no raw keyboard escape API, no xterm ownership, no terminal-brand heuristic, no scan-code surface, no keyboard remapping policy, and no additional public protocol tier.

Traditional input remains the default/fallback. Kitty remains the only actively negotiated modern keyboard protocol in 0.17. xterm `modifyOtherKeys` remains decode-only compatibility.

## Next

T176 — downstream `Icod.DCurses` acceptance through the public 0.17 keyboard APIs.
