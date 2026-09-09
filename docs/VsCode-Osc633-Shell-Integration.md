# VS Code OSC 633 Shell Integration

This document defines the `Icod.Terminal 1.1` contract for the VS Code OSC 633 shell-integration protocol.

OSC 633 is a vendor-specific protocol namespace. It is related to terminal command detection, but it is deliberately separate from the portable OSC 133 semantic-prompt API already exposed by `Icod.Terminal`.

## Supported wire forms

`Icod.Terminal 1.1` emits the documented VS Code forms:

```text
OSC 633 ; A ST
OSC 633 ; B ST
OSC 633 ; C ST
OSC 633 ; D ST
OSC 633 ; D ; exit-code ST
OSC 633 ; E ; command-line [ ; nonce ] ST
OSC 633 ; P ; Cwd=current-directory [ ; nonce ] ST
OSC 633 ; P ; IsWindows=True|False ST
OSC 633 ; P ; HasRichCommandDetection=True|False ST
```

The semantic meanings are:

- `A` — prompt start;
- `B` — prompt end / command-input start;
- `C` — command pre-execution / command-output start;
- `D` — command finished, optionally with a signed decimal exit code;
- `E` — exact caller-supplied command line;
- `P` — a documented shell-integration property.

All emitted frames use canonical ST termination (`ESC \\`).

## Public API

The public surface uses semantic VS Code names rather than exposing marker letters or a generic selector:

```csharp
ValueTask BeginVsCodePromptAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginVsCodeCommandInputAsync(
	CancellationToken cancellationToken = default
);

ValueTask BeginVsCodeCommandOutputAsync(
	CancellationToken cancellationToken = default
);

ValueTask FinishVsCodeCommandAsync(
	int exitCode,
	CancellationToken cancellationToken = default
);

ValueTask AbortVsCodeCommandAsync(
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeCommandLineAsync(
	string commandLine,
	string? nonce = null,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeCurrentDirectoryAsync(
	string currentDirectory,
	string? nonce = null,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeIsWindowsAsync(
	bool isWindows,
	CancellationToken cancellationToken = default
);

ValueTask PublishVsCodeRichCommandDetectionAsync(
	bool hasRichCommandDetection,
	CancellationToken cancellationToken = default
);
```

The methods are independently callable. `Icod.Terminal` does not maintain a synthetic shell-history state machine and does not require that every marker in the canonical sequence be emitted through the same `TerminalSession`.

## Canonical command flow

A caller with rich command detection can compose:

```text
BeginVsCodePromptAsync()
prompt text
BeginVsCodeCommandInputAsync()
user input
PublishVsCodeCommandLineAsync(...)
BeginVsCodeCommandOutputAsync()
command output
FinishVsCodeCommandAsync(exitCode)
```

`PublishVsCodeRichCommandDetectionAsync(true)` is an explicit assertion by the caller that it can provide rich command boundaries. The library does not infer that capability itself.

## Command-line and Cwd serialization

VS Code OSC 633 message values use a protocol-specific escaping scheme before UTF-8 framing.

`Icod.Terminal` applies these rules:

```text
\       -> \\
;       -> \x3b
U+0000 through U+0020 -> \x00 through \x20
```

The hexadecimal digits are lowercase, matching the VS Code serializer. Other well-formed Unicode is encoded as strict UTF-8.

This escaping protects protocol field boundaries. It is not encryption and does not make command lines or paths confidential.

## Nonce handling

`PublishVsCodeCommandLineAsync(...)` and `PublishVsCodeCurrentDirectoryAsync(...)` accept an optional caller-supplied nonce.

`Icod.Terminal`:

- never invents a nonce;
- never reads a nonce from environment variables or terminal process state;
- never stores a nonce as session state;
- validates a supplied nonce as bounded printable ASCII excluding semicolon;
- transmits it only as the documented final OSC 633 field.

A nonce can be used by a VS Code terminal implementation as protocol trust evidence, but it does not provide confidentiality for the published metadata.

## Payload bounds

The complete OSC payload between `ESC ]` and ST is bounded to 65,536 UTF-8 bytes.

The optional nonce is bounded independently to 512 ASCII characters.

Malformed UTF-16 input is rejected before output commitment. Oversized payloads and unsafe nonce values are also rejected before waiting on the session output gate.

## Output, cancellation, and lifecycle

OSC 633 participates in the normal `TerminalSession` output serialization domain.

Each operation:

1. validates semantic arguments;
2. encodes the complete frame;
3. observes caller cancellation;
4. acquires the shared session output gate;
5. observes cancellation again immediately before commit;
6. writes the complete frame with `CancellationToken.None`;
7. does not implicitly flush.

OSC 633 adds no long-lived terminal state and no lifecycle participant. Suspend, resume, and disposal do not automatically replay, finish, abort, or synthesize shell-integration markers.

## Support posture

Successful completion proves only that a complete OSC 633 frame was written to an interactive terminal output endpoint.

The library does not infer support from:

- operating system;
- `TERM`;
- `TERM_PROGRAM` or related environment variables;
- terminal brand or process name;
- terminal version strings.

There is no public automatic VS Code detection/activation path in 1.1.

## Relationship to OSC 133 and OSC 7

OSC 133 remains the portable semantic prompt/command-region API. OSC 633 is not emitted by the OSC 133 methods and the two families are not silently coupled.

OSC 7 remains the preferred portable current-location publication protocol. `PublishVsCodeCurrentDirectoryAsync(...)` is an explicit vendor-specific property publication and never causes automatic OSC 7 emission or process-current-directory discovery.

## Security and privacy

Command lines and current directories may contain credentials, access tokens, hostnames, usernames, filenames, repository locations, or other sensitive information.

`Icod.Terminal` therefore publishes those values only when explicitly supplied by the caller. It does not:

- inspect shell history;
- read process command lines;
- read the process current directory automatically;
- capture environment variables;
- apply heuristic secret redaction;
- modify shell startup files.

Applications remain responsible for deciding whether the metadata is appropriate to disclose to the terminal.

## Deliberately excluded surface

`Icod.Terminal 1.1` does not expose:

- a generic `WriteOsc633Async(...)`, arbitrary marker, or arbitrary property API;
- the unfinalized `F` continuation marker;
- private or undocumented `EnvJson` publication;
- arbitrary environment transfer;
- automatic shell integration installation or startup-file mutation.

These exclusions prevent an additive semantic API from becoming an unbounded vendor-command escape hatch and leave unfinalized protocol extensions available for later dedicated review.
