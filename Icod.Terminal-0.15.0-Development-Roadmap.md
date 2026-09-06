# Icod.Terminal 0.15.0 Development Roadmap

**Project:** `Icod.Terminal`  
**Release line:** `0.15.0`  
**Development version:** `0.15.0`  
**Predecessor:** `0.14.0` — lifecycle-safe color ownership and exact restoration  
**Target frameworks:** `net8.0`; `net9.0`; `net10.0`  
**Language:** C# 13  
**Theme:** OSC 133 extended semantic metadata without weakening the portable core  
**Status:** T150–T156 green; T157 stable closure implemented, exact-head validation pending

---

## 1. Road to 1.0

```text
0.15.0       OSC 133 Extended Metadata
0.16.0       OSC 9 Safe Extensions
0.17.0       modern keyboard contracts and protocols
0.18.0       hardening
1.0.0-rc1    contract freeze and permanent documentation
1.0.0        release closure
```

`net8.0`, `net9.0`, and `net10.0` remain first-class supported targets. Vendor end-of-support alone is not grounds to remove net8/net9; reconsideration requires a concrete security alert, security-fix incompatibility, or equivalent security-maintenance constraint.

---

## 2. Stable 0.15 contract

The portable OSC 133 methods from 0.12 remain byte-for-byte unchanged:

```text
BeginPromptAsync()          -> OSC 133;A ST
BeginCommandInputAsync()    -> OSC 133;B ST
BeginCommandOutputAsync()   -> OSC 133;C ST
FinishCommandAsync(status)  -> OSC 133;D;status ST
AbortCommandAsync()         -> OSC 133;D ST
```

0.15 adds typed extended metadata:

```csharp
TerminalSemanticPromptKind
TerminalSemanticPromptResizeBehavior
TerminalSemanticPromptClickMode
TerminalSemanticPromptOptions
TerminalSemanticCommandOutputOptions

ValueTask BeginPromptAsync(
	TerminalSemanticPromptOptions options,
	CancellationToken cancellationToken = default
);

ValueTask BeginCommandOutputAsync(
	TerminalSemanticCommandOutputOptions options,
	CancellationToken cancellationToken = default
);
```

Prompt mapping:

```text
Secondary                    -> k=s
ShellDoesNotRedrawPrompt     -> redraw=0
UseSpecialCursorKey == true  -> special_key=1
Absolute                     -> click_events=1
Relative                     -> click_events=2
```

Canonical prompt order is `redraw`, `special_key`, `k`, `click_events`.

Command mapping:

```text
CommandLine == null  -> bare C
CommandLine == ""    -> C;cmdline_url=
otherwise            -> C;cmdline_url=<encoded-value>
```

`cmdline_url` validates well-formed Unicode, uses strict UTF-8, leaves only RFC 3986 unreserved ASCII literal, and encodes every other UTF-8 byte as uppercase `%HH`. Maximum OSC 133 payload is 65,536 encoded bytes; oversize metadata is rejected before output and never truncated.

Shell `%q` `cmdline=` remains deliberately excluded.

The stable public delta is frozen in `docs/Public-API-Baseline-0.15.md`.

---

## 3. Lifecycle, ordering, and privacy invariants

- pre-commit cancellation emits nothing;
- committed marker emission is one non-cancellable write with no implicit flush;
- concurrent calls serialize as whole frames through the existing session output gate;
- committed transport failure propagates without compensating OSC traffic;
- no raw caller-supplied OSC 133 keys/values;
- no terminal-brand support oracle;
- no automatic command capture or secret redaction;
- no command parsing/normalization;
- no session-open auto-emission;
- no background OSC 133 listener/support cache;
- no replay on resume;
- no lifecycle ownership lease;
- no synthetic `D` on disposal;
- no OSC 133 traffic from `InvalidateState()`;
- the existing active-query reader/router remains unchanged.

Command-line metadata may contain credentials, tokens, private paths, host names, or other sensitive material. Percent encoding protects framing only and does not provide confidentiality.

---

## 4. Tranche record

### T150 — contract/reference freeze — `0.15.0-alpha.1`

Complete and green at workflow #668.  
Record: `docs/T150-OSC-133-Extended-Metadata-Contract-and-Reference-Freeze.md`.

### T151 — encoder/writer foundation — `0.15.0-alpha.2`

Complete and green at workflow #678.  
Record: `docs/T151-OSC-133-Extended-Metadata-Encoder-and-Writer-Foundation.md`.

### T152 — typed prompt metadata — `0.15.0-alpha.3`

Complete and green at workflow #683.  
Record: `docs/T152-Typed-OSC-133-Prompt-Start-Metadata.md`.

### T153 — typed command-output metadata — `0.15.0-alpha.4`

Complete and green at workflow #689.  
Record: `docs/T153-Typed-OSC-133-Command-Output-Metadata.md`.

### T154 — session integration/compatibility — `0.15.0-alpha.5`

Complete and green at workflow #693.  
Record: `docs/T154-OSC-133-Session-Integration-and-Compatibility.md`.

### T155 — lifecycle/failure/ordering/security hardening — `0.15.0-alpha.6`

Complete and green at workflow #697.  
Record: `docs/T155-OSC-133-Lifecycle-Failure-Ordering-and-Security-Hardening.md`.

### T156 — downstream acceptance — `0.15.0-alpha.7`

Complete and green at workflow #701.

Real `Icod.DCurses 0.1.0` acceptance retains the portable sequence and adds the typed extended sequence through the shared public `TerminalSession` path on net8/net9/net10.  
Record: `docs/T156-DCurses-Extended-OSC-133-Downstream-Acceptance.md`.

### T157 — public API/package/stable closure — `0.15.0`

Implemented; exact stable-head validation pending.

Delivered:

- `docs/Public-API-Baseline-0.15.md`;
- README 0.15 API/interoperability/privacy documentation;
- semantic-prompt sample extended to typed metadata;
- fresh NuGet-only net8/net9/net10 semantic-metadata consumer;
- XML documentation verifier for every new 0.15 public member;
- new 0.15 package gate in PR, distribution/main, and tagged release validation;
- retained 0.8–0.14 package contracts;
- retained/extended real downstream DCurses acceptance;
- stable package release notes/tags;
- prerelease suffix removed.

Record: `docs/T157-0.15.0-Public-API-Package-and-Stable-Closure.md`.

---

## 5. Stable validation gate

Before merge, the exact stable PR head must be green on Windows, Linux, and macOS, including Staging package validation, historical 0.8–0.14 package contracts, the new 0.15 semantic-metadata package contract, and downstream acceptance.

After merge, the exact resulting `main` commit must pass the six-platform Release/distribution matrix before tag `v0.15.0` is created.

The tagged workflow then reruns build/tests, downstream acceptance, exact package selection, historical package contracts, and the 0.15 semantic-metadata package contract before publication.

---

## 6. Explicit non-goals

0.15 does not add `%q` `cmdline=`, arbitrary OSC 133 key/value parameters, OSC 3008, OSC 9 safe extensions, modern keyboard negotiation, terminal-brand auto-detection, shell auto-install/configuration, shell-history/process-command inspection, automatic secret redaction, generic public OSC/CSI/DCS builders, marker lifecycle leases/resume replay, inbound OSC 133 listeners/queries, PTY/ConPTY hosting, terminal emulation, or graphics protocols.

---

## 7. Current stable state

```text
VersionPrefix:    0.15.0
VersionSuffix:
Version:          0.15.0
PackageVersion:   0.15.0
AssemblyVersion:  0.15.0.0
TargetFrameworks: net8.0;net9.0;net10.0
```

**Next:** exact stable-head PR validation. After green validation, merge to `main`, require exact-main Release validation, then tag `v0.15.0`.
