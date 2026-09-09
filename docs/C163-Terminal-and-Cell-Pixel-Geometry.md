# C163 — Terminal and Cell Pixel Geometry

**Release:** `Icod.Terminal 1.6.0`  
**Tranche:** C163  
**Status:** implementation in progress

## 1. Purpose

C163 establishes the pixel-geometry observations required by later raster backends without coupling Sixel or Kitty Graphics to platform-specific console APIs.

The geometry vocabulary distinguishes three facts:

```text
character grid       columns x rows
terminal pixels      width x height
character-cell pixels width x height
```

These facts are related, but they are not interchangeable and are not assumed to originate from the same evidence source.

## 2. Public API decision

C163 keeps the new geometry substrate internal while its semantics are qualified.

Version 1.6 therefore does not yet add a public geometry aggregate, a raw CSI window-operation API, or a new required member on `ITerminalControlProvider`.

This avoids freezing a provider or aggregate contract before the DCS/Sixel and APC/Kitty Graphics tranches exercise it in practice.

## 3. Existing native character-size evidence

`TerminalSession.GetSize()` remains the established character-grid observation and returns `Icod.TermInfo.TerminalSize` through the configured `ITerminalControlProvider`.

C163 does not change that contract.

## 4. POSIX native pixel audit

The POSIX `TIOCGWINSZ` wire structure already contains:

```text
Rows
Columns
PixelWidth
PixelHeight
```

The current `ITerminalControlProvider.GetSize(...)` contract intentionally exposes only `TerminalSize(columns, rows)`, so `PixelWidth` and `PixelHeight` are presently discarded by the public/native size path.

C163 records this as real but currently unexposed native evidence. It does not add a mandatory provider member merely to surface those optional fields.

A future reviewed optional provider contract may expose native pixel dimensions if graphics integration demonstrates that doing so materially improves behavior for custom and system providers. Until then, the portable active-query path is authoritative for pixel observations.

## 5. CSI terminal-pixel query

C163 uses the semantic terminal-window pixel-size request:

```text
CSI 14 t
```

A correlated response is:

```text
CSI 4 ; height ; width t
```

The implementation returns `TerminalPixelSize(width, height)` after validating:

- CSI framing;
- final byte `t`;
- no private parameter bytes;
- no intermediate bytes;
- exactly three numeric parameters;
- response selector `4`;
- positive height and width;
- bounded numeric values.

## 6. CSI cell-pixel query

C163 uses the semantic character-cell pixel-size request:

```text
CSI 16 t
```

A correlated response is:

```text
CSI 6 ; height ; width t
```

The same structural and numeric requirements apply, with required response selector `6`.

Both seven-bit and supported eight-bit CSI responses use the common C160/C161 grammar and semantic conversion layer.

## 7. Correlation

Terminal-pixel and cell-pixel responses share final byte `t`, so correlation is request-specific.

A valid selector-4 response does not satisfy the selector-6 matcher, and vice versa. A malformed response that otherwise plausibly belongs to the active geometry query is retained for deterministic parser failure rather than leaked into ordinary application input.

This is important for late responses and for later graphics probes that may run multiple control-family transactions in one session.

## 8. Exact derivation

A cell size may be derived from character-grid and terminal-pixel observations only when both divisions are exact:

```text
cell width  = terminal pixel width  / columns
cell height = terminal pixel height / rows
```

If either dimension has a remainder, C163 returns no derived value.

The library does not round, truncate, interpolate, or fabricate fractional cell geometry.

## 9. Timeout and support semantics

Geometry queries use the existing authoritative query transaction manager.

Therefore:

- caller cancellation remains cancellation;
- timeout remains an unanswered query, not proof of unsupported geometry reporting;
- malformed correlated responses fail with `FormatException`;
- unrelated input remains on the application input path;
- no second terminal reader is introduced.

## 10. Resource bounds

C163 bounds each reported pixel dimension to `1,000,000`.

The response itself remains subject to the existing CSI framing and query-buffer bounds. This prevents very large decimal fields or malformed responses from bypassing the C160/C161 resource ceilings.

## 11. Graphics boundary

C163 intentionally does not contain:

- Sixel encoding;
- Kitty Graphics APC parsing;
- image placement policy;
- image scaling policy;
- a generic raw `CSI ... t` writer.

Later graphics backends consume semantic pixel geometry rather than implementing their own terminal-size query readers.

## 12. Acceptance

C163 is complete when tests prove:

- exact `CSI 14 t` and `CSI 16 t` request bytes;
- selector-4 and selector-6 response parsing;
- seven-bit and eight-bit response framing;
- height/width wire ordering;
- positive and bounded dimensions;
- request-specific response correlation;
- exact-only cell-size derivation;
- execution through the authoritative `TerminalSession` query path;
- one-reader ownership remains intact;
- no public API change is introduced by C163.
