# T42 — Public API, Documentation, and Sample Audit

**Project:** `Icod.Terminal`  
**Release line:** `0.5.0`  
**Tranche:** T42 — public API, documentation, and sample audit  
**Development version:** `0.5.0-alpha.7`  
**Status:** Complete; stable package closure remains T43

---

## 1. Purpose

T42 reviews the 0.5 public surface after implementation and acceptance evidence from T37–T41, then updates consumer-facing documentation and adds a focused live sample.

No new terminal protocol is introduced in this tranche.

---

## 2. Public API review

The 0.5 public delta is accepted as:

```csharp
public enum TerminalLocationPathStyle {
    Posix = 0,
    WindowsDrive = 1,
    WindowsUnc = 2
}

public ValueTask PublishCurrentLocationAsync(
    string path,
    TerminalLocationPathStyle pathStyle,
    string? authority = null,
    CancellationToken cancellationToken = default
);
```

The review finds this surface appropriately semantic and narrow.

Callers supply native filesystem information; callers do not supply:

- OSC selector numbers;
- OSC framing bytes;
- pre-escaped URI strings;
- arbitrary URI schemes;
- query/fragment data;
- shell-integration protocol tokens.

---

## 3. Process-current-directory convenience decision

The roadmap permitted consideration of a convenience operation which would publish `Environment.CurrentDirectory`.

T42 deliberately rejects adding such a method before stable 0.5.

Reasons:

1. current-location publication can disclose sensitive local path information;
2. the existing API already provides a simple ordinary-use call;
3. passing the path explicitly makes disclosure visible in source code;
4. `Environment.CurrentDirectory` is mutable process-global state rather than session-owned state;
5. no implementation evidence demonstrates that a convenience overload is necessary;
6. omitting it does not prevent a caller from explicitly passing `Environment.CurrentDirectory` when that is truly intended.

This is a public-API restraint decision, not a permanent ban on all future convenience APIs.

---

## 4. Public API baseline

T42 publishes:

```text
docs/Public-API-Baseline-0.5.md
```

The baseline records:

- the exact enum and method signatures;
- emission-oriented success semantics;
- explicit privacy/disclosure behavior;
- deterministic path grammar;
- URI encoding rules visible to callers;
- resource limits;
- rejected API alternatives;
- the boundary between 0.5 current-location publication and 0.6 OSC 8 work.

---

## 5. README update

The repository README now documents the 0.5 development line and adds a focused OSC 7 section covering:

- semantic usage;
- POSIX/Windows/UNC path styles;
- `file:` URI behavior;
- strict UTF-8 percent encoding;
- the 16384-byte URI limit;
- explicit authority disclosure;
- no automatic current-directory publication;
- session output ordering;
- emission-oriented success semantics;
- the 0.5 public API baseline.

The quick-start and sample lists are updated accordingly.

---

## 6. Focused location sample

T42 adds:

```text
samples/Icod.Terminal.Location.Sample/
```

The sample accepts explicit command-line input:

```text
<posix|windows|unc> <absolute-path> [authority]
```

Examples:

```text
posix /usr/local/src
windows C:\Development\Icod
unc \\server\share\project
posix /srv/project example.com
```

The sample intentionally does not read `Environment.CurrentDirectory` automatically. This teaches the privacy model directly.

It also states that successful completion proves frame emission, not terminal-side application.

The sample targets `net8.0`, `net9.0`, and `net10.0`, uses a project reference like the other repository samples, and is included in `Icod.Terminal.sln`.

---

## 7. Regret audit

The following public additions remain rejected for 0.5:

- generic `SendOsc(...)`;
- generic `WriteEscape(...)`;
- public arbitrary URI publication;
- process current-directory mutation;
- automatic process current-directory publication;
- OSC 8 parameters or hyperlink scopes;
- OSC 52 clipboard policy;
- proprietary shell-integration bundles;
- public exposure of the internal URI encoder.

Nothing in the 0.5 public surface requires these later concerns to use the same public abstraction.

---

## 8. Documentation consistency

The documentation now consistently states that:

- OSC 7 is explicit metadata publication;
- local and remote authority information is not inferred from shell/environment state;
- path grammar is caller-selected rather than host-inferred;
- URI escaping is performed by the library exactly once;
- no filesystem canonicalization is performed;
- disposal does not restore or republish location metadata;
- known redirected output rejects the semantic operation;
- successful emission does not establish support.

---

## 9. T42 gate

T42 is complete when:

- the public API baseline is published;
- the README documents the 0.5 contract;
- the focused OSC 7 sample builds in the solution;
- sample documentation teaches explicit disclosure;
- the process-current-directory convenience decision is recorded;
- no premature OSC 8/general shell-integration API is introduced;
- the Windows/Linux/macOS PR matrix is green.

The remaining tranche is **T43 — package, consumer, and stable-release closure**.
