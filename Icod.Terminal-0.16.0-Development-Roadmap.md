# Icod.Terminal 0.16.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.16.0`  
**Development version:** `0.16.0-alpha.1`  
**Predecessor:** `0.15.0` — OSC 133 extended semantic metadata  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** bounded, safe OSC 9 extensions without exposing hazardous terminal control  
**Status:** T160 contract/reference freeze complete; T161 next after exact-head validation

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

## 2. T160 frozen inclusion set

### Existing progress retained unchanged

OSC `9;4` taskbar progress remains exactly the 0.10 semantic progress API/lease. No new progress API is added.

### Legacy OSC 9 notification — included

New semantic operation:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);
```

Wire form:

```text
OSC 9;<message> ST
```

This is the legacy iTerm2 notification form, also documented as compatible by Kitty and WezTerm. Successful emission does not prove a desktop notification was displayed.

### OSC 9;9 Windows current-directory compatibility — included, explicitly secondary

New semantic compatibility operation:

```csharp
ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
	string windowsPath,
	CancellationToken cancellationToken = default
);
```

Wire form:

```text
OSC 9;9;<windowsPath> ST
```

Windows Terminal and ConEmu document this form. The caller supplies a Windows filesystem path.

Existing OSC 7 `PublishCurrentLocationAsync(...)` remains preferred/default and byte-for-byte unchanged. OSC 9;9 is never substituted for OSC 7 and never emitted alongside it automatically. Callers that intentionally need both must call both methods.

The library performs no `wslpath`/`cygpath` conversion, filesystem resolution, current-directory discovery, terminal-brand detection, or environment-variable inspection.

Record: `docs/T160-OSC-9-Safe-Extensions-Contract-and-Reference-Freeze.md`.

---

## 3. Frozen text safety model

Legacy OSC 9 notification and OSC 9;9 have no interoperable payload escaping layer. Encoding printable text as percent/Base64 would alter its visible/semantic value.

Therefore 0.16 uses **strict validation, not transformation**.

For both new operations:

- input must be well-formed UTF-16;
- wire text is strict UTF-8;
- C0 U+0000–U+001F is rejected;
- DEL U+007F is rejected;
- C1 U+0080–U+009F is rejected;
- BEL and ESC are therefore explicitly impossible in payload data;
- printable Unicode, including non-ASCII/non-BMP, is preserved;
- canonical terminator is ST (`ESC \\`);
- validation/size rejection occurs before output commitment;
- payloads are never silently truncated.

Notification-specific semantics:

- `null` -> `ArgumentNullException`;
- empty string is valid;
- whitespace is preserved;
- maximum OSC payload is **4,096 bytes**, including `9;`.

OSC 9;9-specific semantics:

- `null` -> `ArgumentNullException`;
- empty string -> `ArgumentException`;
- path text is preserved without added quotes/normalization;
- maximum OSC payload is **32,768 bytes**, including `9;9;`.

---

## 4. Explicit hazardous/extraneous exclusion set

0.16 SHALL NOT expose ConEmu-family commands for:

- `9;1` sleep/delay;
- `9;2` GUI message box;
- `9;5` wait-for-key;
- `9;6` GUI macro execution;
- `9;7` process launch;
- `9;8` environment-variable disclosure;
- `9;10` xterm emulation mutation.

Also excluded:

- `9;3` tab-title mutation — existing OSC 0/1/2 title APIs already own this semantic;
- `9;11` comments — no useful public semantic operation;
- `9;12` prompt-start — OSC 133 already owns prompt semantics;
- arbitrary OSC 9 command numbers/payloads;
- raw `WriteOsc9Async(...)`;
- generic public OSC builders.

Notification protocols with materially different semantics are separate scope and are not hidden behind `SendNotificationAsync(...)`: Kitty OSC 99, OSC 777, notification IDs/update/close actions, buttons, urgency, sounds, activation reports, and notification support queries are excluded from 0.16.

---

## 5. Output, lifecycle, privacy, and security invariants

Both new operations use the existing `TerminalSession` output serialization domain.

- cancellation before commitment emits nothing;
- committed output is one complete non-cancellable write;
- no implicit flush;
- transport failure propagates without compensating traffic;
- later independent calls remain usable;
- concurrent calls serialize as complete frames;
- no session-open automatic emission;
- no suspend/reset/restore;
- no resume replay;
- no `InvalidateState()` output;
- no disposal synthesis;
- no background OSC 9 reader/probe/cache.

Notifications can expose content to desktop notification history, lock screens, screen sharing, or other observers. Directory hints expose filesystem paths to terminal metadata/history. `Icod.Terminal` does not automatically capture or redact either payload.

---

## 6. Tranche sequence

### T160 — safe-extension contract/reference freeze

**Version:** `0.16.0-alpha.1`  
**Status:** Complete; exact-head validation pending.

Frozen the reference tiers, safe inclusion/exclusion set, public signatures, UTF-8/control policy, ST framing, payload limits, OSC 9;9 relationship to OSC 7, and lifecycle/privacy/security semantics.

### T161 — bounded OSC 9 encoder/writer foundation

**Expected version:** `0.16.0-alpha.2`.

Implement specialized internal encoders/writers only for:

- legacy notification;
- OSC 9;9 Windows-current-directory compatibility.

Prove byte-exact ST framing, Unicode/non-BMP, every rejected control range, exact payload limits, malformed Unicode, cancellation, committed write semantics, and absence of a generic OSC 9 builder.

### T162 — semantic notification API

**Expected version:** `0.16.0-alpha.3`.

Implement `SendNotificationAsync(...)` through the existing session output gate. Prove null/empty/Unicode/control/boundary behavior, no implicit flush, serialization, failure semantics, and unchanged OSC 9;4 progress/title APIs.

### T163 — Windows current-directory compatibility API

**Expected version:** `0.16.0-alpha.4`.

Implement `PublishWindowsCurrentDirectoryCompatibilityAsync(...)`. Prove OSC 7 remains primary/default and byte-for-byte unchanged; no automatic dual emission, path translation, OS restriction, or environment detection occurs.

### T164 — composition and compatibility

**Expected version:** `0.16.0-alpha.5`.

Prove safe OSC 9 operations compose with OSC 9;4 progress, OSC 133 semantic metadata, OSC 7, OSC 0/1/2, synchronized output, hyperlinks/clipboard/pointer/color managers, and active terminal-query/input routing through the shared output serialization domain.

### T165 — lifecycle/failure/ordering/security hardening

**Expected version:** `0.16.0-alpha.6`.

Cover queued cancellation, committed transport failure, concurrent safe OSC 9/progress output, invalidation, suspend/resume, disposal, no replay/synthesis, injection attempts, malformed/oversize text, lock-order/deadlock resistance, and explicit proof that excluded hazardous subcommands are not publicly reachable.

### T166 — downstream acceptance

**Expected version:** `0.16.0-alpha.7`.

Extend real `Icod.DCurses` acceptance so a full-screen consumer coexists with notification and OSC 9;9 publication through public `TerminalSession` APIs. Validate bytes/order deterministically; do not require CI to display a desktop notification.

### T167 — public API/package/stable closure

**Stable version:** `0.16.0`.

Deliver public API baseline, README/sample/security documentation, XML assertions, fresh NuGet-only net8/net9/net10 consumer, retained 0.8–0.15 package gates/downstream acceptance, new 0.16 package contract in PR/main/tag validation, stable metadata, and exact-head release validation.

---

## 7. Required testing matrix

0.16 SHALL prove:

- retained OSC 9;4 byte compatibility;
- exact notification and OSC 9;9 ST frames;
- ASCII/Unicode/non-BMP text;
- malformed UTF-16 rejection;
- every C0/C1/DEL code point rejected before output;
- exact 4,096-byte notification boundary and one-byte-over rejection;
- exact 32,768-byte OSC 9;9 boundary and one-byte-over rejection;
- empty notification allowed;
- empty OSC 9;9 path rejected;
- spaces/punctuation preserved in paths;
- OSC 7 remains unchanged and independent;
- cancellation/failure/concurrency/lifecycle behavior;
- no public hazardous vendor command path;
- Windows/Linux/macOS CI;
- net8/net9/net10 package-only consumers.

Tests validate bytes directly and do not depend on the runner terminal understanding the protocols.

---

## 8. Current development state

```text
VersionPrefix:    0.16.0
VersionSuffix:    alpha.1
Version:          0.16.0-alpha.1
PackageVersion:   0.16.0-alpha.1
AssemblyVersion:  0.16.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next after exact-head T160 validation:** T161 — specialized bounded OSC 9 encoder/writer foundation.
