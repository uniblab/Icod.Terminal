# T151 — OSC 133 Extended-Metadata Encoder and Writer Foundation

**Release:** `Icod.Terminal 0.15.0`  
**Development version:** `0.15.0-alpha.2`  
**Tranche:** T151  
**Status:** Implemented; exact-head validation pending

## Purpose

T151 implements the internal bounded wire-format foundation frozen by T150 without adding any new public API.

The portable 0.12 OSC 133 surface remains untouched.

## Internal encoder

Added:

`src/Output/TerminalOsc133ExtendedMetadataEncoder.cs`

The encoder owns only the metadata forms frozen by T150:

- prompt-start `A` parameters;
- command-output `C;cmdline_url=...`.

It is not a generic OSC 133 key/value builder.

## Prompt metadata

The internal prompt encoder accepts semantic primitive inputs for the future T152 public model:

- shell does not redraw prompt;
- special cursor key declaration;
- secondary prompt;
- click mode 0/1/2.

Canonical output ordering is:

```text
redraw=0
special_key=1
k=s
click_events=1|2
```

Only represented options are emitted.

All-false/zero input is byte-for-byte equivalent to the existing bare `A` frame.

Undefined click values are rejected.

## Command-line metadata

The command-output encoder emits only:

```text
OSC 133;C;cmdline_url=<encoded-text> ST
```

Encoding rules:

1. caller text must be well-formed UTF-16;
2. conversion uses strict UTF-8;
3. RFC 3986 unreserved ASCII bytes remain literal;
4. every other UTF-8 byte becomes uppercase `%HH`;
5. malformed Unicode is rejected rather than replaced;
6. metadata is never truncated.

Shell `%q` `cmdline=` remains excluded.

## Payload bound

The T150 maximum is implemented as:

```csharp
internal const int MaximumPayloadLength = 65_536;
```

The payload count includes `133;A...` or `133;C...` bytes but excludes `ESC ]` and final ST.

The entire frame is sized before allocation/output commitment. Oversized command metadata fails with an argument-family exception.

## Specialized OscWriter integration

`OscWriter.Osc133.cs` now includes internal specialized extended overloads for:

- prompt-start metadata;
- command-output command-line metadata.

The original cached bare `A`, `B`, `C`, and `D` frames remain unchanged.

Extended writers:

- validate arguments at method entry;
- observe cancellation before metadata encoding/output commitment;
- produce one complete frame;
- use the existing noncancellable committed write behavior;
- do not flush.

No generic metadata writer was added.

## Test coverage

Added:

`tests/Icod.Terminal.Tests/src/Output/Osc133ExtendedMetadataWriterTests.cs`

Coverage includes:

- default prompt metadata equals bare `A`;
- canonical multi-parameter ordering;
- every supported prompt field independently;
- invalid click mode rejection;
- empty command line distinct from bare `C`;
- strict UTF-8 percent encoding;
- Unicode and non-BMP input;
- protocol delimiters and shell metacharacters;
- RFC 3986 unreserved literal set;
- malformed UTF-16 rejection;
- exact 65,536-byte payload acceptance;
- one-byte-over rejection;
- one committed noncancellable write;
- no flush;
- pre-cancelled writer emits nothing.

## Public API impact

None.

T152 will introduce the frozen typed prompt model and public prompt overload. T153 will introduce typed command-output metadata and its public overload.

## Next

After exact-head T151 validation is green:

**T152 — typed prompt-start extended metadata.**
