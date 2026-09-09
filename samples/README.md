# Icod.Terminal Samples

The sample projects are small repository consumers built through project references. They teach the supported 1.x usage model; package-only compatibility is validated separately by the consumers under `tools/`, which restore only the freshly packed NuGet artifact.

All samples target `net8.0`, `net9.0`, and `net10.0`.

## Usage rules demonstrated by the samples

The examples follow the permanent 1.x ownership contract:

- a live `TerminalSession` owns the authoritative input reader; use `ReadEventAsync(...)` and typed query methods rather than opening a competing reader;
- use session-managed output APIs for ordinary application and terminal-control output;
- `TerminalSession.Output` is an advanced borrowed transport outside session serialization and is not the normal application-output path;
- use `await using` / `DisposeAsync()` for scoped terminal-state leases so cleanup and restoration remain deterministic;
- exact restoration is used only where the library first observed or captured a truthful baseline;
- terminal-policy reset is not described as exact restoration;
- metadata publication is explicit because paths, user/host identities, shell metadata, clipboard contents, notifications, command lines, and hyperlinks may disclose information outside the application.

## Start here — open a live terminal session

### `Icod.Terminal.Sample`

Minimal session example covering terminal identity, dimensions, application text, and captured native-mode restoration.

```text
dotnet run --project samples/Icod.Terminal.Sample/Icod.Terminal.Sample.csproj -f net10.0
```

Use this sample first when learning session construction and disposal.

## Read terminal input

### `Icod.Terminal.RichInput.Sample`

Interactive event inspector for text, keys, bracketed paste, focus, mouse, and negotiated modern keyboard reporting.

```text
dotnet run --project samples/Icod.Terminal.RichInput.Sample/Icod.Terminal.RichInput.Sample.csproj -f net10.0
```

The sample attempts optional rich-input protocol ownership and retains the traditional keyboard path as the compatibility floor. All input remains on `TerminalSession.ReadEventAsync(...)`.

## Query live terminal state

### `Icod.Terminal.Query.Sample`

Demonstrates explicit bounded Primary/Secondary DA, DSR, CPR, DECRQSS, and XTGETTCAP queries.

```text
dotnet run --project samples/Icod.Terminal.Query.Sample/Icod.Terminal.Query.Sample.csproj -f net10.0
```

Each query has a caller-visible deadline and uses the session's single response-correlation path. Timeout is not treated as proof that a terminal lacks support.

### `Icod.Terminal.Color.Sample`

Demonstrates typed palette/dynamic-color observation and optional exact-restoration ownership.

Observation only:

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0
```

Opt-in scoped mutation:

```text
dotnet run --project samples/Icod.Terminal.Color.Sample/Icod.Terminal.Color.Sample.csproj -f net10.0 -- --mutate
```

The first scoped owner observes the external color before mutation. Final release replays that observed value; OSC 104 and OSC 110–119 remain terminal-policy reset operations instead of restoration substitutes.

## Own reversible presentation or terminal state

### `Icod.Terminal.CursorStyle.Sample`

Typed DECSCUSR cursor-style observation, explicit mutation, and truthful scoped restoration.

```text
dotnet run --project samples/Icod.Terminal.CursorStyle.Sample/Icod.Terminal.CursorStyle.Sample.csproj -f net10.0 -- SteadyUnderline
```

### `Icod.Terminal.SynchronizedOutput.Sample`

Scoped DEC private mode 2026 synchronized-output ownership.

```text
dotnet run --project samples/Icod.Terminal.SynchronizedOutput.Sample/Icod.Terminal.SynchronizedOutput.Sample.csproj -f net10.0
```

The lease brackets terminal-side presentation timing; it is not an application-side byte buffer.

### `Icod.Terminal.Progress.Sample`

Scoped terminal progress covering determinate, indeterminate, error/attention state, nesting, and cleanup.

```text
dotnet run --project samples/Icod.Terminal.Progress.Sample/Icod.Terminal.Progress.Sample.csproj -f net10.0
```

### `Icod.Terminal.PointerShape.Sample`

OSC 22 pointer-shape mutation, scoped ownership, nested fallback, explicit terminal-policy reset, and bounded pointer queries.

```text
dotnet run --project samples/Icod.Terminal.PointerShape.Sample/Icod.Terminal.PointerShape.Sample.csproj -f net10.0
```

Pointer-shape reset intentionally returns control to terminal policy; it does not claim knowledge of an arbitrary pre-Icod pointer shape.

## Publish semantic terminal metadata

### `Icod.Terminal.Title.Sample`

Semantic OSC 0/1/2 icon/window title operations.

```text
dotnet run --project samples/Icod.Terminal.Title.Sample/Icod.Terminal.Title.Sample.csproj -f net10.0
```

### `Icod.Terminal.Location.Sample`

Preferred portable OSC 7 current-location publication plus the explicit Windows Terminal/ConEmu OSC 9;9 compatibility form.

Portable location example:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- posix /usr/local/src
```

Explicit Windows compatibility example:

```text
dotnet run --project samples/Icod.Terminal.Location.Sample/Icod.Terminal.Location.Sample.csproj -f net10.0 -- windows-osc9 C:\work\repo
```

OSC 7 remains the preferred location API. The sample does not inspect the process current directory, detect terminal brand, translate WSL/Cygwin paths, or emit both protocols automatically.

### `Icod.Terminal.SemanticPrompt.Sample`

Portable and typed OSC 133 prompt/command-region metadata.

```text
dotnet run --project samples/Icod.Terminal.SemanticPrompt.Sample/Icod.Terminal.SemanticPrompt.Sample.csproj -f net10.0
```

The sample demonstrates prompt, command-input, command-output, explicit completion, abort, typed prompt metadata, and explicit command-line metadata. Command lines can contain secrets; publication is caller policy and is not automatically redacted.

### `Icod.Terminal.ITerm2ShellIntegration.Sample`

Typed iTerm2 OSC 1337 shell-integration and semantic-history metadata. All metadata is supplied explicitly; the sample does not read process current directory, user name, host name, shell environment, or shell startup files.

```text
dotnet run --project samples/Icod.Terminal.ITerm2ShellIntegration.Sample/Icod.Terminal.ITerm2ShellIntegration.Sample.csproj -f net10.0 -- /srv/repo alice host.example.test bash 20 branch main
```

To demonstrate the explicitly destructive `ClearCapturedOutput` operation, add the opt-in flag:

```text
dotnet run --project samples/Icod.Terminal.ITerm2ShellIntegration.Sample/Icod.Terminal.ITerm2ShellIntegration.Sample.csproj -f net10.0 -- /srv/repo alice host.example.test bash 20 branch main --clear-captured-output
```

OSC 7 remains the preferred portable current-location API; this sample demonstrates the separate iTerm2-specific metadata path. User-variable Base64 encoding is wire framing, not confidentiality.

The focused iTerm2 sample is built by `packaging/VerifyITerm2ShellIntegrationSample.ps1` on every supported TFM during repository validation.

### `Icod.Terminal.Notification.Sample`

Demonstrates three explicit desktop-notification protocols. The sample never selects a protocol from terminal branding.

Legacy OSC 9 example:

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- "Build complete"
```

Explicit OSC 777 titled example:

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- --titled "Build" "Compilation complete"
```

Explicit Kitty OSC 99 example:

```text
dotnet run --project samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj -f net10.0 -- --kitty "Build" "Compilation complete"
```

The Kitty example supplies only explicit title/body plus a fixed sample application/type label. It does not query terminal identity, read process state, or activate host-native notification APIs. Successful completion means the selected protocol frame(s) were emitted, not that the desktop displayed them. Notification content may appear in notification history, lock screens, screen sharing, multiplexed sessions, or terminal logs.

The focused notification sample is built by `packaging/VerifyNotificationSample.ps1` on every supported TFM during repository validation.

## Publish interactive content

### `Icod.Terminal.Hyperlink.Sample`

Bounded OSC 8 hyperlink output and scoped hyperlink ownership.

```text
dotnet run --project samples/Icod.Terminal.Hyperlink.Sample/Icod.Terminal.Hyperlink.Sample.csproj -f net10.0 -- https://example.com/ "example link" example-1
```

The library validates and emits a URI; it does not decide whether a scheme is trustworthy or activate the target. Applications that need a scheme allow-list must enforce it themselves.

### `Icod.Terminal.Clipboard.Sample`

Explicit OSC 52 clipboard/selection writes and privacy-sensitive reads.

```text
dotnet run --project samples/Icod.Terminal.Clipboard.Sample/Icod.Terminal.Clipboard.Sample.csproj -f net10.0 -- "copied text"
```

Clipboard reads are never automatic. Terminal-side security policy may ignore or deny them, and a timeout is not interpreted as permanent lack of support.

## Choosing a sample

For ordinary terminal-aware applications, a useful progression is:

```text
Icod.Terminal.Sample
    -> Icod.Terminal.RichInput.Sample
    -> Icod.Terminal.Query.Sample
    -> one focused state/output sample relevant to the application
```

Higher-level full-screen applications normally consume these contracts through `Icod.DCurses` rather than reimplementing cell/window/refresh policy directly.
