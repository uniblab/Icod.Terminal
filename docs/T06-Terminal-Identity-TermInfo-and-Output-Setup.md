# T06 — Terminal Identity, TermInfo Integration, and Output Setup

**Project:** `Icod.Terminal`
**Development line:** `0.1.0`
**Development version:** `0.1.0-alpha.6`
**Tranche:** T06 — Terminal identity, `Icod.TermInfo` integration, and output setup
**Reference branch:** `Icod.Terminal/initial_add`
**Implementation status:** Implemented

---

## 1. Purpose

T06 binds the reversible live session introduced in T05 to an explicit immutable
`Icod.TermInfo.TerminalDescription` and establishes the session output contract.

T03-T05 deliberately separated live operating-system state from terminal
capability data. T06 joins those layers without changing their ownership:

- `Icod.TermInfo` remains the authority for terminal descriptions, discovery,
  parameter expansion, padding, byte-fidelity rules, built-in profiles, and the
  reversible Windows virtual-terminal output helper;
- `Icod.Terminal` owns the live session which selects a description, configures
  host output state where required, and carries terminal/application bytes;
- higher layers such as `Icod.DCurses` consume the selected description and the
  session output APIs without performing platform-specific terminal detection or
  Windows console-mode setup.

---

## 2. Terminal identity

Every open `TerminalSession` now exposes:

```csharp
TerminalIdentity Identity
TerminalDescription Terminal
```

`TerminalIdentity` records:

- the selected immutable `TerminalDescription`;
- the requested terminal name, when resolution began with one;
- the `TerminalIdentitySource` describing how selection completed.

The sources are:

```text
ExplicitOverride
NamedProfile
PlatformFallback
```

This makes fallback observable rather than silently pretending an unknown
terminal is a different named terminal.

---

## 3. Resolution precedence

`TerminalSessionOptions` now accepts:

```csharp
TerminalDescription? TerminalOverride
string? TerminalName
TerminalDatabase? TerminalDatabase
```

`TerminalOverride` and `TerminalName` are mutually exclusive.

Resolution proceeds as follows:

1. an explicit `TerminalOverride` wins without database lookup;
2. otherwise `TerminalName`, when supplied, is the requested name;
3. otherwise the current `TERM` value from `Icod.TermInfo.TerminalEnvironment`
   is used when present;
4. the requested name is resolved through the configured database;
5. when no database is supplied, the session composes a fresh
   `SystemTerminalDescriptionProvider` followed by
   `TerminalDatabase.BuiltIn`;
6. a missing or unsupported name uses the platform fallback.

Provider failures are not converted into misses. The failure semantics of
`Icod.TermInfo` remain intact.

### 3.1 Unknown names

An unknown POSIX terminal name falls back to `TerminalProfiles.Dumb`.

It does **not** silently become `xterm`, `xterm-256color`, or another
feature-rich profile merely because those terminals are common.

### 3.2 Windows fallback

For a Windows console endpoint, fallback uses the existing TermInfo profile
model:

- Windows Terminal sessions identified by a nonblank `WT_SESSION` use
  `TerminalProfiles.MsTerminalDirect`;
- other Windows console sessions use `TerminalProfiles.WinConsole`.

This is profile selection only. It does not itself mutate the Windows console
output mode.

---

## 4. Reversible Windows output setup

`TerminalSessionOptions.ConfigureOutput` defaults to `true`.

For a system-backed live Windows console session, initialization acquires the
existing:

```csharp
Icod.TermInfo.WindowsVirtualTerminal.TryEnableOutput(...)
```

lease for process standard output or standard error.

The session owns that lease as terminal **state**, while continuing to borrow the
output byte service and stream lifetime.

Cleanup order is:

```text
flush pending terminal output
        |
        v
restore Windows VT output lease, when present
        |
        v
restore captured input mode
```

The lease is disposed exactly once through the same cached T05 restoration
path. Initialization failure after baseline capture still triggers rollback.

Custom `ITerminalControlProvider` implementations do not cause
`Icod.Terminal` to mutate the process standard console behind the provider's
back. Such providers retain responsibility for any equivalent transport/output
setup.

For a system provider using a non-standard Windows console endpoint, automatic
VT setup is intentionally rejected because the published TermInfo helper owns
standard output/error semantics. A caller may set `ConfigureOutput = false`
only when that caller-owned endpoint is already configured.

---

## 5. Application text encoding

T06 establishes an explicit application-text policy.

`TerminalSessionOptions.ApplicationEncoding` defaults to strict UTF-8 without a
byte-order mark:

```csharp
new UTF8Encoding(
    encoderShouldEmitUTF8Identifier: false,
    throwOnInvalidBytes: true)
```

The session clones the supplied encoding at open time and exposes a snapshot
through `TerminalSession.ApplicationEncoding`.

Application text is emitted with:

```csharp
await session.WriteTextAsync(text, cancellationToken);
```

Applications may select another encoding explicitly through session options.

---

## 6. TermInfo protocol-byte fidelity

Terminfo capability strings are **not application text**.

`Icod.TermInfo` represents compiled one-byte capability data through .NET
strings and documents `Encoding.Latin1` as the byte-preserving transport
encoding. T06 follows that contract independently of the session application
encoding.

An already-resolved or expanded terminal string is emitted with:

```csharp
await session.WriteTerminalStringAsync(
    value,
    affectedLines,
    cancellationToken);
```

The implementation routes through:

```csharp
TermInfoOutput.TPutsAsync(...)
```

with:

- `Encoding.Latin1` for exact capability bytes;
- the selected `TerminalDescription`;
- the captured POSIX output baud rate when representable;
- the configured TermInfo padding mode;
- the optional caller-supplied delay provider.

This preserves TermInfo's padding and byte-output semantics instead of
reimplementing them inside `Icod.Terminal`.

---

## 7. Direct capability output

For a non-parameterized standard string capability, callers may use:

```csharp
bool written = await session.WriteCapabilityAsync(
    StringCapability.EnterCursorAddressingMode);
```

The method returns `false` when the selected terminal does not advertise the
capability and emits no bytes.

Parameterized capabilities remain owned by the immutable TermInfo description:
callers expand them through `TerminalDescription.Expand(...)`, then pass the
result to `WriteTerminalStringAsync(...)`. This keeps parameter semantics in
`Icod.TermInfo` while keeping live output transport in `Icod.Terminal`.

---

## 8. Padding policy

`TerminalSessionOptions.CapabilityPaddingMode` defaults to:

```csharp
PaddingMode.Delay
```

An optional `CapabilityDelayProvider` may be supplied for deterministic or
specialized delay handling.

When the captured POSIX mode reports a positive output baud rate within the .NET
integer range, the session supplies that rate to `TermInfoOutputOptions`.
Windows console snapshots and unrecognized POSIX speed codes simply leave the
baud rate unspecified.

---

## 9. Ownership and rollback

T06 does not change the T05 resource-ownership contract.

The session still borrows:

- `ITerminalControlProvider`;
- input/output endpoints;
- `ITerminalInput`;
- `ITerminalOutput`.

It owns reversible live terminal state acquired during initialization:

- the semantic input-mode transition;
- the system Windows VT-output lease, when applicable.

Any failure after baseline capture enters the same deterministic rollback path.
Repeated `DisposeAsync()` calls do not replay cleanup.

---

## 10. Test coverage

T06 tests are deterministic and use injected terminal-control and byte-output
services. They cover:

- explicit terminal-description override precedence;
- named built-in database resolution;
- unknown POSIX fallback to `dumb`;
- Windows Console / Windows Terminal fallback selection;
- strict UTF-8 application-text output;
- Latin-1 terminal-protocol byte fidelity independent of application encoding;
- direct present-capability emission;
- absent-capability behavior;
- ambiguous or invalid identity/output options.

The existing `Icod.TermInfo` test suite remains the authority for the internal
reversibility of `WindowsVirtualTerminal` itself.

---

## 11. Gate result and next tranche

T06 satisfies the roadmap gate: a standard live session now owns terminal
identity resolution and Windows output setup, exposes the selected
`TerminalDescription`, and emits capability-driven terminal output without
requiring `Icod.DCurses` to perform platform-specific terminal discovery or
Windows VT setup.

T07 can now build resize and lifecycle observation on an already identified,
configured, reversible `TerminalSession`.
