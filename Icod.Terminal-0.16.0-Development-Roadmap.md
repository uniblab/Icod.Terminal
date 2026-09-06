# Icod.Terminal 0.16.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.16.0`  
**Development version:** `0.16.0-alpha.5`  
**Predecessor:** `0.15.0` — OSC 133 extended semantic metadata  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** bounded, safe OSC 9 extensions without exposing hazardous terminal control  
**Status:** T160–T163 green; T164 composition implemented, exact-head validation pending

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

0.16 deliberately does not mean “support every vendor command numbered OSC 9.” OSC 9 is fragmented. The release exposes only bounded, non-interactive, non-executing semantic operations appropriate for a terminal library.

---

## 2. Frozen public scope

Existing OSC `9;4` taskbar progress remains exactly the 0.10 semantic progress API/lease.

0.16 adds:

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

Notification emits legacy `OSC 9;<message> ST`.

OSC `9;9;<windowsPath> ST` is explicitly secondary Windows Terminal/ConEmu compatibility. Existing OSC 7 `PublishCurrentLocationAsync(...)` remains preferred/default and unchanged. Neither API automatically emits the other.

The library performs no path translation, filesystem/CWD discovery, terminal-brand detection, or environment inspection.

---

## 3. Frozen text safety model

Both new operations require well-formed UTF-16 and emit strict UTF-8. C0 U+0000–U+001F, DEL U+007F, and C1 U+0080–U+009F are rejected before output; printable Unicode including non-BMP text is preserved. Canonical termination is ST (`ESC \\`). Payloads are fully validated/measured before commitment and never truncated.

Notification:

- null rejected;
- empty allowed;
- 4,096-byte OSC payload maximum including `9;`.

OSC 9;9:

- null/empty rejected;
- path preserved without normalization/quoting;
- 32,768-byte OSC payload maximum including `9;9;`.

---

## 4. Explicit exclusion set

0.16 does not expose ConEmu-family sleep/delay (`9;1`), GUI message box (`9;2`), wait-for-key (`9;5`), GUI macro (`9;6`), process launch (`9;7`), environment disclosure (`9;8`), or xterm-emulation mutation (`9;10`).

Also excluded are `9;3` title duplication, `9;11` comments, `9;12` prompt duplication, arbitrary OSC 9 command numbers/payloads, raw/generic OSC writers, Kitty OSC 99, OSC 777, notification IDs/update/close/buttons/sounds/urgency/activation reports, and notification support queries.

---

## 5. Output/lifecycle/privacy invariants

Both new operations use the existing `TerminalSession` output serialization domain.

- pre-commit cancellation emits nothing;
- committed output is one non-cancellable write;
- no implicit flush;
- failures propagate without compensation;
- concurrent operations serialize as complete frames;
- no automatic session-open emission;
- no suspend/reset/restore or resume replay;
- no `InvalidateState()` output;
- no disposal synthesis;
- no background OSC 9 reader/probe/cache.

Notification text and directory paths can expose sensitive information to terminal/desktop metadata/history. The library neither captures nor redacts these values automatically.

---

## 6. Tranche record

### T160 — safe-extension contract/reference freeze — `0.16.0-alpha.1`

Complete and green at workflow #717.  
Record: `docs/T160-OSC-9-Safe-Extensions-Contract-and-Reference-Freeze.md`.

### T161 — bounded encoder/writer foundation — `0.16.0-alpha.2`

Complete and green at workflow #722.

Specialized internal strict-text encoders/writers were added only for notification and OSC 9;9; retained OSC 9;4 progress was untouched.  
Record: `docs/T161-OSC-9-Safe-Text-Encoder-and-Writer-Foundation.md`.

### T162 — semantic notification API — `0.16.0-alpha.3`

Complete and green at workflow #726.

`SendNotificationAsync(...)` is public through the existing session output gate with bounded validation, ST framing, pre-commit cancellation, one non-cancellable committed write, and no implicit flush.  
Record: `docs/T162-Semantic-OSC-9-Notification-API.md`.

### T163 — Windows current-directory compatibility API — `0.16.0-alpha.4`

Complete and green at workflow #730.

`PublishWindowsCurrentDirectoryCompatibilityAsync(...)` is public as an explicitly secondary OSC 9;9 compatibility operation. OSC 7 remains preferred/default and independent.  
Record: `docs/T163-Windows-Current-Directory-OSC-9-9-Compatibility-API.md`.

### T164 — composition and compatibility — `0.16.0-alpha.5`

Implemented; exact-head validation pending.

No production changes were required. Dedicated integration tests prove notification and OSC 9;9 compose in caller order with titles, OSC 7, OSC 133, hyperlinks, clipboard, pointer shape, palette/dynamic-color mutation, retained OSC 9;4 progress, synchronized output, and the active query/router path.  
Record: `docs/T164-OSC-9-Safe-Extension-Composition-and-Compatibility.md`.

### T165 — lifecycle/failure/ordering/security hardening — `0.16.0-alpha.6`

Next after green T164 validation.

Cover queued cancellation, committed transport failure, concurrent safe OSC 9/progress output, invalidation, suspend/resume, disposal, no replay/synthesis, injection attempts, malformed/oversize text, lock-order/deadlock resistance, and explicit proof that excluded hazardous subcommands are not publicly reachable.

### T166 — downstream acceptance — `0.16.0-alpha.7`

Extend real `Icod.DCurses` acceptance so a full-screen consumer coexists with notification and OSC 9;9 publication through public `TerminalSession` APIs. Validate bytes/order deterministically; CI does not need to display a desktop notification.

### T167 — public API/package/stable closure — `0.16.0`

Deliver public API baseline, README/sample/security documentation, XML assertions, fresh NuGet-only net8/net9/net10 consumer, retained 0.8–0.15 package gates/downstream acceptance, new 0.16 package contract in PR/main/tag validation, stable metadata, and exact-head release validation.

---

## 7. Required testing matrix

0.16 SHALL prove retained OSC 9;4 byte compatibility; exact notification/OSC 9;9 ST frames; ASCII/Unicode/non-BMP text; malformed UTF-16 and every C0/C1/DEL rejection; exact 4,096/32,768-byte boundaries; empty notification and rejected empty path; printable path preservation; OSC 7 independence; cancellation/failure/concurrency/lifecycle behavior; no public hazardous vendor command path; Windows/Linux/macOS CI; and net8/net9/net10 package-only consumers.

Tests validate bytes directly and do not depend on the runner terminal understanding the protocols.

---

## 8. Current development state

```text
VersionPrefix:    0.16.0
VersionSuffix:    alpha.5
Version:          0.16.0-alpha.5
PackageVersion:   0.16.0-alpha.5
AssemblyVersion:  0.16.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T164 validation:** T165 — lifecycle/failure/ordering/security hardening.
