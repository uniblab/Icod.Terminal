# T80 — Cursor-Style Contract and Reference Freeze

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.1`  
**Status:** Contract frozen for implementation  
**Primary protocol reference:** DEC VT520 DECSCUSR — Set Cursor Style  
**Compatibility reference:** xterm control sequences — DECSCUSR and DECRQSS  
**Existing query substrate:** `TerminalStatusStringKind.CursorStyle`, DECRQSS identifier `SP q`

---

## 1. Purpose

T80 freezes the protocol mapping, compatibility boundary, query interpretation,
support semantics, and restoration rules for cursor-style control before production
implementation begins.

The canonical control is DEC Set Cursor Style (DECSCUSR):

```text
CSI Ps SP q
```

`Icod.Terminal 0.8.0` SHALL expose semantic cursor styles rather than raw numeric
parameters or arbitrary CSI construction.

Cursor style and cursor visibility are separate state. DECSCUSR changes the shape
and blinking behavior of an enabled text cursor; it does not replace DECTCEM or
alter the existing cursor-visibility contract.

---

## 2. Reference behavior

The DEC VT520 reference defines:

```text
Ps omitted, 0, or 1  blinking block (default)
Ps 2                 steady block
Ps 3                 blinking underline
Ps 4                 steady underline
```

xterm implements those values and extends DECSCUSR with:

```text
Ps 5                 blinking bar
Ps 6                 steady bar
Ps 7                 restore xterm's initial cursor resource state
```

The `5` and `6` bar forms are useful de-facto terminal extensions and are in scope
for 0.8. The `7` form is not itself a cursor style; it is an xterm-specific state
restoration command and is deliberately excluded from the generic cursor-style
value set.

---

## 3. Frozen semantic cursor-style set

The semantic model SHALL contain exactly these six styles:

```text
BlinkingBlock
SteadyBlock
BlinkingUnderline
SteadyUnderline
BlinkingBar
SteadyBar
```

Final public type naming remains subject to T83/T87 API review, but the semantic
set itself is frozen by T80.

The model SHALL NOT contain:

```text
Default
Reset
Initial
Restore
Hidden
Visible
Raw
Unknown
```

`Hidden` and `Visible` belong to cursor visibility, not cursor style. `Default`,
`Reset`, `Initial`, and `Restore` would conflate state-setting with restoration
semantics which differ between DEC and xterm. `Raw` and `Unknown` would expose
protocol representation rather than a closed semantic contract.

---

## 4. Frozen outbound mapping

Library-generated DECSCUSR SHALL use explicit numeric parameters:

| Semantic style | Outbound `Ps` | Reference status |
| --- | ---: | --- |
| Blinking block | `1` | DEC core |
| Steady block | `2` | DEC core |
| Blinking underline | `3` | DEC core |
| Steady underline | `4` | DEC core |
| Blinking bar | `5` | xterm extension |
| Steady bar | `6` | xterm extension |

The setter SHALL emit canonical seven-bit CSI framing:

```text
ESC [ 1 SP q
ESC [ 2 SP q
ESC [ 3 SP q
ESC [ 4 SP q
ESC [ 5 SP q
ESC [ 6 SP q
```

The library SHALL NOT emit an omitted parameter or `Ps = 0` from the semantic
setter. Although DEC defines omitted/`0` as blinking block, an explicit `1` gives
the same frozen semantic result without using an overloaded default form.

The library SHALL NOT emit `Ps = 7` from the generic cursor-style setter.

Outbound DECSCUSR SHALL NOT use C1 CSI framing.

---

## 5. Parameter `0` and the meaning of "default"

T80 explicitly rejects a generic `Default` cursor-style value.

DEC defines omitted/`0`/`1` as blinking block. Therefore `0` is a control-function
default parameter whose resulting DEC cursor style is blinking block. It is not a
portable instruction meaning "restore the user's previous cursor style" or
"restore emulator startup preferences."

For outbound semantic operations:

```text
BlinkingBlock -> Ps = 1
```

For inbound typed observation:

```text
omitted Ps -> BlinkingBlock
Ps = 0     -> BlinkingBlock
Ps = 1     -> BlinkingBlock
```

Collapsing these three forms is semantically lossless for the DEC cursor-style
contract because they describe the same visible/blinking state.

---

## 6. xterm parameter `7`

xterm defines `Ps = 7` as restoring the terminal's initial cursor state from xterm
resource settings such as cursor blink, underline, and bar configuration.

0.8 freezes the following policy:

- `7` is not a member of the semantic cursor-style enum/value type;
- `SetCursorStyleAsync(...)` SHALL never emit `7`;
- no generic `ResetCursorStyleAsync()` operation SHALL be introduced in 0.8 merely
  as a wrapper around `7`;
- `7` SHALL NOT be used to implement scoped restoration;
- a positive typed query which reports `7` instead of an actual recognized style
  SHALL be treated as an unrecognized cursor-style state, not silently mapped to a
  six-style value.

A future explicitly xterm-specific API may reconsider initial-resource restoration
if real consumer evidence justifies it.

---

## 7. Bar-cursor compatibility policy

Blinking and steady bar styles (`Ps = 5` and `Ps = 6`) are xterm extensions rather
than DEC VT520 core styles.

They are nevertheless included in the 0.8 semantic set because they represent
real cursor styles, not a different operation, and are widely useful on modern
xterm-compatible terminals.

Their presence in the public semantic set SHALL NOT be represented as proof that a
particular endpoint supports them.

The library SHALL NOT:

- infer bar-style support solely from `TERM`;
- infer bar-style support solely from operating system;
- infer bar-style support solely from emulator identity;
- add a cached `SupportsBarCursor` Boolean without positive protocol evidence;
- silently substitute block or underline when a caller requests a bar style.

Successful setter completion proves emission only.

---

## 8. DECRQSS query contract

0.8 SHALL reuse the existing DECRQSS transaction substrate.

The request identifier is already frozen as:

```text
SP q
```

and the existing public operation:

```csharp
QueryStatusStringAsync(
    TerminalStatusStringKind.CursorStyle,
    timeout,
    cancellationToken
)
```

remains source- and behavior-compatible.

The typed 0.8 query SHALL be layered on this transport rather than adding another
reader, response matcher, or private query loop.

A successful DECRQSS response contains the corresponding DECSCUSR status string.
The typed parser SHALL accept exactly one optional numeric `Ps` followed by
`SP q`.

Recognized semantic mappings are:

```text
SP q       -> BlinkingBlock
0 SP q     -> BlinkingBlock
1 SP q     -> BlinkingBlock
2 SP q     -> SteadyBlock
3 SP q     -> BlinkingUnderline
4 SP q     -> SteadyUnderline
5 SP q     -> BlinkingBar
6 SP q     -> SteadyBar
```

The parser MAY accept leading decimal zeroes when they still represent a single
numeric parameter, because CSI numeric parameter syntax is numeric rather than a
canonical textual serialization contract.

The following are malformed or unrecognized typed cursor-style responses and SHALL
fail deterministically with `FormatException`:

- more than one parameter;
- non-decimal parameter data;
- extra intermediate bytes;
- wrong final byte;
- a recognized DECRQSS wrapper carrying the wrong status-string identifier;
- numeric values greater than `6` including xterm command value `7`;
- numeric overflow;
- otherwise syntactically malformed returned state.

The existing bounded DECRQSS frame/status-string limits remain authoritative.

---

## 9. Typed query result semantics

0.8 SHALL introduce a typed cursor-style observation shape rather than returning a
nullable style directly.

Semantically the result contains:

```text
IsSupported
Style
```

with these invariants:

```text
IsSupported == true  => Style contains one of the six frozen semantic styles
IsSupported == false => Style is absent
```

Final public type/member names remain T84/T87 work.

A negative DECRQSS response is a normal explicit unsupported observation and SHALL
not be converted to `FormatException`.

Caller-visible timeout, cancellation, transport failure, and malformed positive
response remain distinct failure modes under the existing query transaction
contract.

No typed cursor-style query occurs automatically during session open, capability
discovery, lifecycle handling, suspend/resume, or disposal.

---

## 10. Setter support and endpoint semantics

The semantic setter requires an interactive terminal output endpoint under the
same policy as existing semantic terminal-control output.

Known redirected/incompatible output is rejected before DECSCUSR emission.

No terminal query is required before an ordinary setter call. Requiring a query
would make a simple state mutation depend on a response mechanism which many
terminals may not expose even when they accept DECSCUSR.

Therefore:

- setter success means a complete DECSCUSR frame was emitted;
- setter success does not prove the terminal honored the requested style;
- no automatic fallback style is emitted;
- no automatic retry occurs;
- a query timeout does not prove that setting cursor style is unsupported.

---

## 11. Output ownership and cancellation

DECSCUSR writes SHALL use the existing session-owned control-output serialization
path.

The complete small control frame SHALL be constructed and validated before output
commit.

Before commit:

- invalid style values are rejected;
- caller cancellation may prevent all output.

After commit:

- caller cancellation SHALL NOT truncate a DECSCUSR frame;
- the ordinary setter SHALL NOT add an implicit output flush unless implementation
  evidence proves one is required.

The setter must compose in deterministic order with text output, OSC title/location/
hyperlink/clipboard operations, presentation transitions, and active queries.

---

## 12. Truthful restoration contract

T80 freezes the meaning of restoration even though T85 decides whether a public
lease is justified.

**Exact cursor-style restoration means emitting the semantic style which was
observed immediately before library-owned cursor-style mutation.**

The following are not exact restoration:

- emitting `Ps = 0`;
- emitting `Ps = 1` without knowing the previous style;
- emitting xterm `Ps = 7`;
- guessing from `TERM` or emulator identity;
- restoring a hard-coded block cursor;
- using cursor visibility state as a proxy for cursor style.

A future cursor-style lease may be introduced only if acquisition can obtain an
authoritative previous semantic style before mutation.

If acquisition requires a query and that query:

- returns unsupported;
- times out;
- is cancelled;
- returns malformed state;
- reports an unrecognized value;

then lease acquisition SHALL NOT mutate cursor style and SHALL fail according to
the corresponding established operation semantics.

T85 retains authority to decline a lease entirely if implementation evidence shows
that this contract is too costly or unreliable for the public surface.

---

## 13. Nested ownership if a lease is later accepted

T80 does not yet mandate a lease, but it freezes the only acceptable restoration
model if T85 introduces one.

Nested cursor-style ownership must be stack-like:

```text
observe A
acquire B -> set B
acquire C -> set C
release C -> restore B
release B -> restore A
```

An inner lease restores the style owned immediately below it, not necessarily the
terminal style originally observed at the outermost acquisition.

Out-of-order release SHALL NOT silently mutate tracked state.

The final lifecycle/retry mechanics remain T85/T86 work.

---

## 14. Cursor visibility independence

DECSCUSR and DECTCEM are orthogonal.

Setting cursor style SHALL NOT:

- show a hidden cursor;
- hide a visible cursor;
- acquire/release the existing cursor-visibility presentation lease;
- redefine cursor visibility state;
- infer visibility from DECRQSS cursor-style data.

Existing cursor-visibility APIs and behavior SHALL remain unchanged by 0.8.

Tests in T86 must explicitly prove this independence.

---

## 15. CSI output-layer boundary

DECSCUSR is the release driver for an internal structural CSI writer capable of
representing:

```text
parameter bytes
intermediate bytes
final byte
```

The DECSCUSR space is an intermediate byte and SHALL be represented structurally,
not embedded as a magic literal in the public session method.

0.8 SHALL NOT expose the structural CSI writer publicly.

The public API remains semantic even though T81 establishes a reusable internal
primitive for later protocol work such as 0.9 synchronized output.

---

## 16. Security and resource posture

Cursor-style operations carry no caller-controlled arbitrary text payload and do
not require new large buffers.

The important safety properties are therefore protocol integrity and ownership:

- closed typed style set;
- no raw numeric setter;
- no arbitrary CSI parameter/intermediate/final injection;
- no implicit query/probing;
- bounded existing DECRQSS response parsing;
- no support fabrication;
- no guessed restoration.

No 0.8 cursor-style operation changes the 0.7 OSC 52 input-buffer limits.

---

## 17. Public API boundary

T80 freezes semantics, while final spelling remains T83/T84/T87 work.

The expected public delta is limited to concepts equivalent to:

```csharp
public enum TerminalCursorStyle {
    BlinkingBlock,
    SteadyBlock,
    BlinkingUnderline,
    SteadyUnderline,
    BlinkingBar,
    SteadyBar
}

public ValueTask SetCursorStyleAsync(
    TerminalCursorStyle style,
    CancellationToken cancellationToken = default
);

public ValueTask<TerminalCursorStyleObservation> QueryCursorStyleAsync(
    TimeSpan timeout,
    CancellationToken cancellationToken = default
);
```

A lease/acquisition API is intentionally not frozen here; T85 must prove that it
can satisfy the restoration contract before it becomes public.

0.8 SHALL NOT expose:

```text
SetCursorStyle(int)
SetCursorStyle(string)
SendCsi(...)
WriteEscape(...)
ResetCursorStyle() backed by xterm Ps=7
Default cursor-style enum member
raw DECRQSS cursor-style parser APIs
background cursor-style monitoring
```

---

## 18. Required implementation tests

T81/T82 SHALL provide byte/codec tests for at least:

```text
BlinkingBlock     -> ESC [ 1 SP q
SteadyBlock       -> ESC [ 2 SP q
BlinkingUnderline -> ESC [ 3 SP q
SteadyUnderline   -> ESC [ 4 SP q
BlinkingBar       -> ESC [ 5 SP q
SteadyBar         -> ESC [ 6 SP q
```

Typed query interpretation must test:

- omitted parameter;
- `0` and `1` aliases;
- `2` through `6`;
- leading-zero numeric forms;
- explicit negative DECRQSS response;
- `7` rejection;
- value greater than `7` rejection;
- multi-parameter rejection;
- malformed status-string rejection;
- timeout/cancellation/late-response behavior through the existing query manager.

T86 SHALL additionally prove cursor-visibility independence, semantic-output
ordering, and lifecycle compatibility.

---

## 19. T80 gate

T80 is complete when implementation proceeds under these invariants:

1. the semantic style set is exactly six styles: four DEC core styles plus two
   xterm bar extensions;
2. outbound semantic style mapping uses explicit `Ps = 1` through `6`;
3. omitted/`0`/`1` inbound state all map to blinking block;
4. no public generic `Default`, `Reset`, `Initial`, or `Restore` style exists;
5. xterm `Ps = 7` is not a cursor style and is not used for generic restoration;
6. typed query interpretation reuses the existing DECRQSS `SP q` transaction path;
7. negative DECRQSS means explicit unsupported observation;
8. malformed or unrecognized positive state fails with `FormatException`;
9. setter success proves emission, not terminal support or terminal-side execution;
10. cursor style and cursor visibility remain independent;
11. any future lease must restore a previously observed semantic style rather than
    emit a guessed reset;
12. no public raw CSI or raw numeric cursor-style escape hatch is introduced.

The next tranche is **T81 — reusable CSI intermediate-byte output primitive**.
