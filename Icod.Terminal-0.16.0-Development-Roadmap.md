# Icod.Terminal 0.16.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.16.0`  
**Development version:** `0.16.0-alpha.7`  
**Predecessor:** `0.15.0` — OSC 133 extended semantic metadata  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** bounded, safe OSC 9 extensions without exposing hazardous terminal control  
**Status:** T160–T165 green; T166 downstream acceptance implemented, exact-head validation pending

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

0.16 adds only:

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

Both new operations use strict UTF-8, reject C0/DEL/C1 controls, use ST termination, validate before output, and never truncate. Notification is bounded to 4,096 OSC payload bytes; OSC 9;9 is bounded to 32,768 payload bytes.

The library performs no path translation, CWD discovery, terminal-brand detection, environment inspection, or automatic secret redaction.

---

## 3. Explicit exclusion set

0.16 does not expose sleep/delay, GUI message boxes, wait-for-key, GUI macros, process launch, environment disclosure, xterm-emulation mutation, duplicate title/prompt commands, arbitrary OSC 9 command numbers/payloads, raw/generic OSC writers, Kitty OSC 99, OSC 777, notification IDs/update/close/buttons/sounds/urgency/activation reports, or notification support queries.

---

## 4. Output/lifecycle/privacy invariants

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

Hardening coverage proves queued cancellation, committed failure recovery, notification/progress whole-frame serialization, invalidation/suspend/resume/disposal non-replay, and a public-surface audit excluding hazardous/generic OSC 9 entry points. Existing T161–T163 tests retain malformed/oversize/control/injection boundary coverage.  
Record: `docs/T165-OSC-9-Safe-Extension-Lifecycle-Failure-Ordering-and-Security-Hardening.md`.

### T166 — downstream acceptance — `0.16.0-alpha.7`
Implemented; exact-head validation pending.

The existing real `Icod.DCurses 0.1.0` semantic-prompt acceptance project now retains the portable/extended OSC 133 sequences and appends notification → real `RefreshAsync()` → OSC 9;9 → real `RefreshAsync()` → notification, using only public `TerminalSession` APIs and the same shared session. The retained verifier already runs this project on net8/net9/net10.  
Record: `docs/T166-DCurses-Safe-OSC-9-Downstream-Acceptance.md`.

### T167 — public API/package/stable closure — `0.16.0`

Next after green T166 validation.

Deliver the 0.16 public API baseline, README/sample/security docs, XML assertions, fresh NuGet-only net8/net9/net10 consumer, retained 0.8–0.15 gates/downstream acceptance, a new 0.16 package contract in PR/main/tag validation, stable metadata, and exact-head release validation.

---

## 6. Current development state

```text
VersionPrefix:    0.16.0
VersionSuffix:    alpha.7
Version:          0.16.0-alpha.7
PackageVersion:   0.16.0-alpha.7
AssemblyVersion:  0.16.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T166 validation:** T167 — public API/package/documentation/stable closure.
