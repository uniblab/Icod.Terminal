# Icod.Terminal 0.16.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.16.0`  
**Development version:** `0.16.0`  
**Predecessor:** `0.15.0` — OSC 133 extended semantic metadata  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** bounded, safe OSC 9 extensions without exposing hazardous terminal control  
**Status:** T160–T167 complete; T167A pre-merge polish implemented, exact-head validation pending

---

## 1. Position on the road to 1.0

```text
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

---

## 2. Frozen public scope

Existing OSC `9;4` progress remains unchanged.

0.16 adds exactly:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);

ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
	string windowsPath,
	CancellationToken cancellationToken = default
);
```

Notification emits legacy `OSC 9;<message> ST`. OSC `9;9;<windowsPath> ST` is explicitly secondary Windows Terminal/ConEmu compatibility. OSC 7 remains preferred/default and independent.

Both new operations use strict UTF-8, reject C0/DEL/C1 controls, use ST termination, validate complete bounded frames before waiting for the shared output gate, and never truncate. Notification is bounded to 4,096 OSC payload bytes; OSC 9;9 is bounded to 32,768 payload bytes.

The library performs no path translation, CWD discovery, terminal-brand detection, environment inspection, or automatic secret redaction.

Stable public contract: `docs/Public-API-Baseline-0.16.md`.

---

## 3. Explicit exclusion set

0.16 does not expose sleep/delay, GUI message boxes, wait-for-key, GUI macros, process launch, environment disclosure, xterm-emulation mutation, duplicate title/prompt commands, arbitrary OSC 9 command numbers/payloads, raw/generic OSC writers, Kitty OSC 99, OSC 777, notification IDs/update/close/buttons/sounds/urgency/activation reports, or notification support queries.

---

## 4. Output/lifecycle/privacy invariants

- validation and complete frame encoding occur before waiting for the output gate;
- pre-commit cancellation emits nothing;
- committed output is one non-cancellable write;
- no implicit flush;
- failures propagate without compensation;
- concurrent operations serialize as complete frames through the existing session output gate;
- no session-open automatic emission;
- no suspend/reset/restore or resume replay;
- no `InvalidateState()` output;
- no disposal synthesis;
- no background OSC 9 reader/probe/cache.

Notification text and directory paths may expose sensitive information to terminal/desktop metadata/history. The caller controls publication.

---

## 5. Tranche record

### T160 — contract/reference freeze — `0.16.0-alpha.1`
Complete and green at workflow #717.  
Record: `docs/T160-OSC-9-Safe-Extensions-Contract-and-Reference-Freeze.md`.

### T161 — bounded encoder/writer foundation — `0.16.0-alpha.2`
Complete and green at workflow #722.  
Record: `docs/T161-OSC-9-Safe-Text-Encoder-and-Writer-Foundation.md`.

### T162 — semantic notification API — `0.16.0-alpha.3`
Complete and green at workflow #726.  
Record: `docs/T162-Semantic-OSC-9-Notification-API.md`.

### T163 — Windows current-directory compatibility API — `0.16.0-alpha.4`
Complete and green at workflow #730.  
Record: `docs/T163-Windows-Current-Directory-OSC-9-9-Compatibility-API.md`.

### T164 — composition and compatibility — `0.16.0-alpha.5`
Complete and green at workflow #735 after correcting a test-only byte-prefix assertion compile error.

Dedicated integration tests prove composition with titles, OSC 7, OSC 133, hyperlinks, clipboard, pointer shape, colors, OSC 9;4 progress, synchronized output, and active query routing.  
Record: `docs/T164-OSC-9-Safe-Extension-Composition-and-Compatibility.md`.

### T165 — lifecycle/failure/ordering/security hardening — `0.16.0-alpha.6`
Complete and green at workflow #739.

Hardening coverage proves queued cancellation, committed failure recovery, notification/progress whole-frame serialization, invalidation/suspend/resume/disposal non-replay, and a public-surface audit excluding hazardous/generic OSC 9 entry points.  
Record: `docs/T165-OSC-9-Safe-Extension-Lifecycle-Failure-Ordering-and-Security-Hardening.md`.

### T166 — downstream acceptance — `0.16.0-alpha.7`
Complete and green at workflow #743.

The existing real `Icod.DCurses 0.1.0` acceptance project retains the portable/extended OSC 133 sequences and appends notification → real `RefreshAsync()` → OSC 9;9 → real `RefreshAsync()` → notification, using only public `TerminalSession` APIs and the same shared session on net8/net9/net10.  
Record: `docs/T166-DCurses-Safe-OSC-9-Downstream-Acceptance.md`.

### T167 — public API/package/stable closure — `0.16.0`
Complete.

Delivered the stable public baseline, README/API/privacy documentation, explicit Location sample compatibility mode, fresh NuGet-only package consumer, XML/package verifier, retained 0.8–0.15 contracts, extended DCurses acceptance, stable package metadata, and PR/main/tag gates.  
Record: `docs/T167-0.16.0-Public-API-Package-and-Stable-Closure.md`.

### T167A — pre-merge polish — `0.16.0`
Implemented; exact-head validation pending.

The final audit adds no public API. It strengthens the release by:

- validating/encoding complete notification and OSC 9;9 frames before waiting for the session output gate;
- proving malformed/control/oversize input fails without queuing behind an occupied output gate;
- exhaustively testing all 65 forbidden C0/DEL/C1 code points for both operations;
- testing exact and one-byte-over payload limits with multibyte UTF-8 content;
- adding a focused `Icod.Terminal.Notification.Sample` with privacy guidance;
- building that sample on net8/net9/net10 in PR, distribution/main, and tag validation;
- documenting the `windows-osc9` Location sample mode and OSC 7 precedence;
- extending the fresh packed-artifact consumer to reflect over the shipped public surface and reject raw/generic/hazardous OSC 9 entry points.

Record: `docs/T167A-0.16.0-Pre-Merge-Polish.md`.

---

## 6. Stable release gate

Before merge, the exact stable PR head must be green on Windows, Linux, and macOS, including the focused notification-sample build, Staging package validation, historical 0.8–0.15 package contracts, the new 0.16 safe OSC 9 package/exclusion contract, and downstream acceptance.

After merge, the exact resulting `main` commit must pass Release/distribution validation before tag `v0.16.0` is created.

The tagged workflow then reruns build/tests, the notification sample build, downstream acceptance, exact package selection, historical package contracts, and the 0.16 safe OSC 9 package contract before publication.

---

## 7. Current stable state

```text
VersionPrefix:    0.16.0
VersionSuffix:
Version:          0.16.0
PackageVersion:   0.16.0
AssemblyVersion:  0.16.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** exact stable-head PR validation. After green validation, merge to `main`, require exact-main Release validation, then tag `v0.16.0`.
