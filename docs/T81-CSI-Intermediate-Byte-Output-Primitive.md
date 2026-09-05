# T81 — Reusable CSI Intermediate-Byte Output Primitive

**Project:** `Icod.Terminal`  
**Release line:** `0.8.0`  
**Development version:** `0.8.0-alpha.2`  
**Status:** Implemented; PR validation pending  
**Predecessor:** T80 — cursor-style contract and reference freeze

---

## 1. Purpose

T81 introduces the internal structural CSI output primitive required by DECSCUSR
without exposing a public raw-CSI construction API.

The canonical seven-bit CSI shape is:

```text
ESC [ <parameter bytes> <intermediate bytes> <final byte>
```

For cursor style this produces:

```text
ESC [ Ps SP q
```

The implementation deliberately separates CSI structural fields so later internal
protocol work can reuse the same validated framing rules without embedding ad hoc
escape literals inside semantic session methods.

---

## 2. Internal primitive

`CsiWriter.EncodeFrame(...)` accepts three structural fields:

- parameter bytes in the inclusive range `0x30` through `0x3F`;
- intermediate bytes in the inclusive range `0x20` through `0x2F`;
- one final byte in the inclusive range `0x40` through `0x7E`.

It always emits canonical seven-bit CSI introduction:

```text
ESC [
```

No C1 CSI output form is generated.

The primitive is internal. T81 does not create `SendCsi(...)`, `WriteEscape(...)`,
or any other caller-controlled raw control-sequence surface.

---

## 3. DECSCUSR integration

`CsiWriter.EncodeCursorStyleFrame(int)` is limited to the T80 frozen explicit
cursor-style parameters:

```text
1  blinking block
2  steady block
3  blinking underline
4  steady underline
5  blinking bar
6  steady bar
```

The generated frames are exactly:

```text
ESC [ 1 SP q
ESC [ 2 SP q
ESC [ 3 SP q
ESC [ 4 SP q
ESC [ 5 SP q
ESC [ 6 SP q
```

Parameter `0`, omitted parameters, xterm parameter `7`, and arbitrary numeric
values are not emitted by this helper.

Semantic cursor-style typing remains T82 work.

---

## 4. Output commit semantics

`CsiWriter.WriteCursorStyleAsync(...)` follows the established semantic-output
commit policy:

1. validate output and parameter;
2. honor caller cancellation before transmission commits;
3. build the complete frame before output;
4. honor caller cancellation again before the transport write;
5. once committed, write the complete frame with `CancellationToken.None` so
   ordinary caller cancellation cannot deliberately truncate the control frame.

The operation does not flush implicitly.

Public session-owned output serialization remains T83 work; T81 proves only the
internal framing/emission primitive.

---

## 5. Tests

`CsiWriterTests` verifies:

- byte-exact DECSCUSR output for parameters `1` through `6`;
- structural encoding with parameter, intermediate, and final fields;
- rejection of parameter bytes outside `0x30`–`0x3F`;
- rejection of intermediate bytes outside `0x20`–`0x2F`;
- rejection of final bytes outside `0x40`–`0x7E`;
- rejection of cursor-style parameters outside `1`–`6`;
- one complete non-cancellable transport write after commit;
- cancellation before commit produces zero writes.

---

## 6. T81 gate

T81 is complete when:

1. CSI output fields are structurally encoded rather than concatenated as ad hoc
   escape strings;
2. only canonical seven-bit CSI output is generated;
3. field byte ranges are validated before frame construction;
4. DECSCUSR parameters `1` through `6` produce byte-exact frames;
5. invalid cursor-style parameters produce zero output;
6. cancellation cannot intentionally truncate a committed frame;
7. no public raw CSI API is introduced;
8. Windows, Linux, and macOS PR validation is green.

The next tranche is **T82 — typed cursor-style codec and DECRQSS interpretation**.
