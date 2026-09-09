# iTerm2 OSC 1337 Shell Integration

This document is the permanent 1.x authority for the typed iTerm2 OSC 1337 surface added in `Icod.Terminal 1.3.0`.

OSC 1337 is an iTerm2 extension namespace containing many unrelated operations. `Icod.Terminal` does **not** expose that namespace as a generic command dispatcher. Version 1.3 deliberately implements only the reviewed shell-integration and semantic-history metadata core.

## Public API

```csharp
ValueTask SetITerm2MarkAsync(
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2CurrentDirectoryAsync(
	string currentDirectory,
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2RemoteHostAsync(
	string userName,
	string hostName,
	CancellationToken cancellationToken = default
);

ValueTask SetITerm2UserVariableAsync(
	string name,
	string value,
	CancellationToken cancellationToken = default
);

ValueTask PublishITerm2ShellIntegrationVersionAsync(
	int version,
	string shellName,
	CancellationToken cancellationToken = default
);

ValueTask ClearITerm2CapturedOutputAsync(
	CancellationToken cancellationToken = default
);
```

## Wire mapping

The methods emit canonical ST-terminated OSC frames:

```text
SetITerm2MarkAsync()
    -> OSC 1337;SetMark ST

PublishITerm2CurrentDirectoryAsync(path)
    -> OSC 1337;CurrentDir=<path> ST

PublishITerm2RemoteHostAsync(user, host)
    -> OSC 1337;RemoteHost=<user>@<host> ST

SetITerm2UserVariableAsync(name, value)
    -> OSC 1337;SetUserVar=<name>=<base64(utf8(value))> ST

PublishITerm2ShellIntegrationVersionAsync(version, shell)
    -> OSC 1337;ShellIntegrationVersion=<version>;shell=<shell> ST

ClearITerm2CapturedOutputAsync()
    -> OSC 1337;ClearCapturedOutput ST
```

The current version-plus-shell form is used for shell-integration version publication. The older version-only form is not exposed.

## Encoding and bounds

All textual content is validated before output commitment.

- Text must be well-formed Unicode.
- Outbound text is encoded using strict UTF-8.
- C0, DEL, and C1 controls are rejected where raw text fields are accepted.
- The complete OSC payload is bounded to 65,536 encoded bytes.
- Remote-host user/host components are independently bounded and reject `@`, `;`, and `=` delimiters.
- User-variable names are bounded and reject `;` and `=` delimiters.
- User-variable values are strict UTF-8 bytes encoded as Base64, matching iTerm2's shell-integration convention.
- Shell names are bounded ASCII identifiers using letters, digits, `.`, `_`, `-`, or `+`.
- Shell-integration versions are non-negative decimal integers.

`CurrentDir` preserves caller-supplied printable text rather than normalizing a path. Semicolon and equals are therefore allowed in the current-directory value; OSC framing controls are not.

## Output and cancellation contract

The complete frame is constructed before the operation waits for the shared `TerminalSession` output gate.

Pre-commit cancellation emits no frame. Once output is committed, the complete frame is written through one non-cancellable transport write so caller cancellation cannot split an OSC frame. The operations do not implicitly flush.

Known redirected/non-terminal output is rejected.

Successful completion proves only that the complete frame was written to the configured terminal output. It does not prove iTerm2 received, trusted, retained, or acted on the metadata.

## Explicit metadata and privacy

OSC 1337 shell metadata can reveal sensitive information:

- `CurrentDir` can disclose user names, repositories, customer/project names, mounts, and remote paths.
- `RemoteHost` explicitly discloses a user and host identity.
- `SetUserVar` can publish arbitrary application-provided values to terminal-visible state.
- `ShellIntegrationVersion` reveals the caller's integration version and shell family.

`Icod.Terminal` does not read `Environment.CurrentDirectory`, `Environment.UserName`, host names, process arguments, shell history, environment variables, or shell startup files automatically to populate these values. Publication is explicit caller intent.

Base64 protects the `SetUserVar` wire grammar; it is **not encryption or redaction**.

## Relationship to OSC 7 and OSC 133

OSC 1337 does not replace the portable semantic APIs.

- `PublishCurrentLocationAsync(...)` / OSC 7 remains the preferred portable current-location publication operation.
- OSC 133 remains the portable prompt/command-region metadata family.
- OSC 1337 `CurrentDir` and semantic-history operations are explicitly iTerm2-specific.

Calling one family never silently emits another family. The library does not infer iTerm2 support from `TERM`, `TERM_PROGRAM`, host OS, process name, or version strings.

## Lifecycle and restoration

These operations are ephemeral metadata, not reversible terminal-state ownership.

They add no lifecycle participant or restoration lease. They are not automatically replayed after resume. Disposal does not invent a mark, current-directory update, remote-host update, shell-version update, or captured-output clear.

`ClearITerm2CapturedOutputAsync(...)` is intentionally destructive to iTerm2's current semantic-history captured-output metadata. It is never emitted automatically.

## Deliberate OSC 1337 exclusions

The iTerm2 OSC 1337 namespace contains operations whose effects are materially different from shell metadata. Version 1.3 intentionally excludes public operations for:

- generic arbitrary OSC 1337 command or parameter dispatch;
- profile switching or profile-property mutation;
- focus stealing;
- URL/browser opening;
- pasteboard access through OSC 1337;
- file upload/download, inline-file transfer, or upload requests;
- custom script control sequences;
- arbitrary color mutation where existing typed color APIs own the semantic contract;
- cursor-shape mutation where the existing typed pointer/cursor APIs own the contract;
- Unicode-version mutation;
- Touch Bar key-label mutation;
- arbitrary variable-reporting queries.

These omissions preserve the 1.x rule that a vendor protocol is exposed only through a bounded semantic API with a reviewed security and ownership contract.
