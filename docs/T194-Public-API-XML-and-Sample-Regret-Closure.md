# T194 — Public API, XML, and Sample Regret Closure

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T193 — permanent output/presentation/security semantics, workflow #925  
**Status:** Complete and green  
**Exact-head validation:** workflow #947 at `86ff0cfc923314aca35bcf8fb731a97bd0ade526`

## 1. Purpose

T194 turns the 1.0 regret audit into enforceable source-level compatibility and reconciles public XML/sample guidance with the permanent T191–T193 contract.

No new terminal protocol is added. No additional D-class API correction was found.

## 2. Exact public-surface capture

The deterministic reflection snapshot was captured from exact head:

`510f9738f216064f80193eba1936583b5552170e`

Workflow #926 generated the public surface independently under:

```text
net8.0
net9.0
net10.0
```

All three logical snapshots were identical.

The snapshot contains:

```text
633 lines
77 exported public types
32 public enums
242 public enum values
124 public methods
144 public properties
13 public constructors
```

The normalized SHA-256 is:

```text
8b213bb287e14729b07f0e640c8c1b1a5aa36b26f867f1e97604fb86fded36e5
```

The fingerprint is stored in `docs/Public-API-Baseline-1.0-rc1.sha256`; the consumer-facing explanation is `docs/Public-API-Baseline-1.0-rc1.md`.

## 3. Enforcement

T194 adds `packaging/VerifyPublicApiBaseline.ps1`.

The verifier:

1. runs the deterministic reflection generator for net8.0, net9.0, and net10.0;
2. normalizes line endings before fingerprinting;
3. fails if any supported TFM exposes a different public surface;
4. fails if the common surface differs from the frozen rc1 SHA-256;
5. leaves the generated text snapshots in validation artifacts for inspection.

The gate is wired into PR Staging validation and `VerifyDistribution.ps1`, so Release/main validation also enforces it.

The initial enforcement wiring exposed two PowerShell-only verifier defects: an error-message parser expression and a three-path array construction. Neither changed the library or fingerprint. Both were corrected before the exact T194 closure run.

## 4. T190 breaking correction represented permanently

The snapshot freezes the sole T190 D-class correction:

- `ITerminalInput` remains public;
- custom `OpenAsync(..., ITerminalInput, ITerminalOutput, ...)` remains public;
- `TerminalSession.Input` is absent from the public surface;
- `TerminalSession.Output` remains public as the documented advanced borrowed output transport.

No further public removal, rename, or signature correction was justified during T194.

## 5. Enum numeric-value freeze

Every current public enum value is part of the fingerprint.

This intentionally freezes the current rc1 numeric layout rather than attempting to resurrect superseded early-pre-1.0 layouts.

Important anchors include:

```text
TerminalInputEventKind: Text=0, Key=1, Mouse=2, Focus=3, Paste=4, EndOfInput=5
TerminalEventKind: Input=0, Lifecycle=1, Timeout=2, Cancelled=3
TerminalKeyEventPhase: Press=0, Repeat=1, Release=2
TerminalKeyboardReportingMode: Disambiguated=0, EventTypes=1, AllKeys=2
TerminalKeyModifiers: None=0, Shift=1, Control=2, Alt=4, Super=8, Hyper=16, Meta=32, CapsLock=64, NumLock=128
```

The complete `TerminalKey` vocabulary and every other public enum are equally covered by the machine gate.

## 6. Public XML cleanup

The package-generated XML audit found public wording that described permanent behavior using historical release labels such as “0.3 query milestone,” “0.7 ceiling,” “0.8 contract,” “0.13 contract,” and “0.15/0.16 bound.”

T194 rewrites those consumer-visible descriptions as permanent semantic/resource contracts. Covered areas include CSI/DECRQSS/XTGETTCAP query summaries, maximum undecoded-input buffering, cursor/pointer/color summaries, OSC 7 location conversion, OSC 9 bounds, and OSC 133 metadata ordering/bounds.

These changes are documentation/runtime-message cleanup only. They do not change public signatures or protocol behavior.

## 7. Sample audit

The existing sample set already covers the important 1.x workflows: session open/dispose, unified rich input, active typed queries, cursor style, synchronized output, progress, pointer shape, palette/dynamic color observation and ownership, titles, location, hyperlinks, clipboard, semantic prompt metadata, and desktop notification.

The samples compile with the T190 removal of public `TerminalSession.Input`, proving none depends on the unsafe raw-reader escape hatch.

The audit found no reason to add another sample solely for rc1. The missing problem was navigation and permanent guidance, not workflow coverage.

## 8. Task-oriented sample index

`samples/README.md` is rewritten around consumer tasks and ownership semantics rather than release chronology.

It now teaches one authoritative session input reader, typed query routing, `await using` for scoped state, exact restoration vs terminal-policy reset, semantic session output rather than raw borrowed output, explicit privacy/disclosure choices, and which sample to start from for a new application.

## 9. No additional sample/API expansion

T194 deliberately does not add a sample for raw `TerminalSession.Output` writes, internal lifecycle race machinery, generic escape construction, or a second input reader; nor does it add a new protocol/convenience API merely for catalog symmetry.

## 10. Validation

Workflow #947 passed at exact head `86ff0cfc923314aca35bcf8fb731a97bd0ade526`.

The closure gate passed:

- Windows/Linux/macOS build and tests;
- the frozen public API fingerprint gate;
- real `Icod.DCurses` focused acceptance and hardening soak;
- exact Staging package verification;
- every retained package-only contract from 0.8 through 0.18.

T194 is therefore complete. T195 owns permanent compatibility/versioning and migration policy.
