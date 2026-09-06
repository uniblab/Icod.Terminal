# T162 — Semantic OSC 9 Notification API

**Release:** `Icod.Terminal 0.16.0`  
**Tranche:** T162  
**Version:** `0.16.0-alpha.3`  
**Status:** Implemented; exact-head validation pending

## Delivered

T162 exposes the T160-approved legacy OSC 9 notification form through a semantic `TerminalSession` API:

```csharp
ValueTask SendNotificationAsync(
	string message,
	CancellationToken cancellationToken = default
);
```

The method is a thin public layer over the T161 bounded writer and uses the existing session output serialization domain.

## Contract

- `null` message -> `ArgumentNullException`;
- empty message is valid;
- well-formed Unicode is encoded as strict UTF-8;
- C0, DEL, and C1 controls are rejected before output;
- maximum payload is 4,096 encoded bytes including `9;`;
- canonical wire form is ST-terminated `OSC 9;<message> ST`;
- no implicit flush;
- pre-commit cancellation emits nothing;
- committed transport write is non-cancellable;
- successful completion proves emission only, not that a desktop notification was displayed;
- no terminal-brand detection or capability probe;
- no automatic notification generation.

## Tests

Public/session tests cover:

- ordinary ASCII message;
- empty message;
- Unicode/non-BMP text;
- null rejection;
- newline, tab, BEL, ESC, DEL, and C1 rejection;
- malformed UTF-16 rejection;
- exact 4,096-byte boundary;
- one-byte-over rejection;
- pre-cancellation;
- waiting behind the shared control-output serialization lease;
- non-interactive output rejection;
- non-cancellable committed write and no implicit flush.

## Scope discipline

T162 does not expose OSC 9;9 yet and does not change the existing OSC 9;4 progress API. Rich notification protocols such as Kitty OSC 99 remain outside 0.16.

Next after green exact-head validation: T163 — explicitly named Windows current-directory OSC 9;9 compatibility API.
