# T163 — Windows Current-Directory OSC 9;9 Compatibility API

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T163  
**Version:** `0.16.0-alpha.4`  
**Status:** Implemented; exact-head validation pending

## Public API

```csharp
ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
	string windowsPath,
	CancellationToken cancellationToken = default
);
```

The API emits the bounded T161 form:

```text
OSC 9;9;<windowsPath> ST
```

## Contract

- caller supplies the Windows filesystem path explicitly;
- null is rejected;
- empty path is rejected;
- well-formed printable Unicode is preserved and encoded as strict UTF-8;
- C0, DEL, and C1 controls are rejected before output;
- maximum encoded OSC payload is 32,768 bytes including `9;9;`;
- oversize input is rejected and never truncated;
- no path normalization, separator rewriting, filesystem lookup, symlink resolution, or existence check;
- no `wslpath`/`cygpath`/MSYS translation;
- no process-CWD discovery;
- no terminal-brand or environment-variable detection;
- no operating-system restriction is imposed on the caller;
- one complete ST-terminated frame is emitted through the existing session output serialization domain;
- pre-commit cancellation emits nothing;
- committed write is non-cancellable;
- no implicit flush.

## OSC 7 relationship

The existing `PublishCurrentLocationAsync(...)` OSC 7 API remains the preferred/default current-location semantic API and is unchanged.

T163 does not modify that method. Calling OSC 7 emits only OSC 7. Calling the compatibility method emits only OSC 9;9. Applications that intentionally need both protocols must call both explicitly.

This distinction is encoded in the long public method name so OSC 9;9 is not mistaken for the general current-location abstraction.

## Tests

Public/session tests prove:

- byte-exact ASCII path publication;
- spaces and printable punctuation preservation;
- Unicode/non-BMP UTF-8 preservation;
- no path normalization/translation;
- null/empty rejection;
- control and malformed-Unicode rejection;
- exact 32,768-byte boundary;
- one-byte-over rejection;
- pre-cancellation;
- serialization behind an existing output owner;
- non-interactive endpoint rejection;
- explicit independence of OSC 7 and OSC 9;9.

## Non-goals

T163 does not add a location protocol selector, automatic fallback, terminal detection, dual-emission helper, path conversion service, or generic OSC 9 writer.

Next after a green exact T163 head: T164 — composition and compatibility across the existing session managers and protocols.
