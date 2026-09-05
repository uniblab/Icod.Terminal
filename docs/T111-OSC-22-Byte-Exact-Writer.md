# T111 — OSC 22 Byte-Exact Pointer-Shape Writer

**Release:** `0.11.0`  
**Tranche:** `T111`  
**Development version:** `0.11.0-alpha.2`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T111 implements the internal byte-exact OSC 22 pointer-shape transport frozen by T110 without yet adding a public pointer-shape API.

---

## 2. Canonical framing

Base semantic set operations emit:

```text
ESC ] 22 ; shape ESC \
```

Terminal-policy reset emits:

```text
ESC ] 22 ; ESC \
```

The writer uses explicit String Terminator (`ESC \\`) rather than BEL as the canonical 0.11 outbound form.

---

## 3. Canonical-name validation

The internal base writer accepts only the 30 CSS-compatible names frozen by T110.

It rejects:

- empty strings;
- arbitrary xterm/X11 names such as `hand2`;
- differently-cased names;
- comma-separated lists;
- Kitty stack syntax such as `>wait`;
- Kitty query syntax such as `?__current__`;
- unknown names.

`null` is reserved internally for terminal-policy reset and encodes an empty payload.

---

## 4. Commit and cancellation semantics

For a write:

1. output is validated;
2. caller cancellation is checked;
3. the complete OSC 22 frame is encoded;
4. caller cancellation is checked again before commit;
5. one output write is issued using `CancellationToken.None`.

The writer never deliberately exposes a caller-cancellable partially committed OSC frame.

No implicit flush occurs.

---

## 5. Tests

`tests/Icod.Terminal.Tests/src/Output/Osc22PointerShapeWriterTests.cs` proves:

- byte-exact ST framing for representative canonical names;
- exact empty-payload reset framing;
- rejection of raw X11 names, Kitty query/stack syntax, lists, casing variants, empty and unknown names;
- exactly one transport write;
- committed transport receives a non-cancellable token;
- no implicit flush;
- pre-cancelled writes emit nothing;
- invalid names emit nothing.

---

## 6. Public-surface effect

T111 adds no public API.

The internal string form exists only as the boundary between the future semantic pointer value mapping and the byte encoder. T112 will introduce the typed semantic model; public arbitrary pointer-name strings remain prohibited.

---

## 7. T111 decision

The byte-level OSC 22 setter/reset path is implemented and ready for exact-head validation.

T112 may proceed only after cumulative `0.11.0-alpha.2` validation confirms the new wire layer compiles and tests cleanly across the supported matrix.
