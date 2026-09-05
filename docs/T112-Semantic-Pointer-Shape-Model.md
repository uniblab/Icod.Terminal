# T112 — Semantic Pointer-Shape Model

**Release:** `0.11.0`  
**Tranche:** `T112`  
**Development version:** `0.11.0-alpha.3`  
**Status:** Implemented; exact-head validation required

---

## 1. Purpose

T112 introduces the frozen public semantic vocabulary for OSC 22 terminal mouse-pointer shapes and a single canonical codec shared by setters, leases, and later explicit query parsing.

This tranche does not add session ownership or public setter/lease methods; those remain T113/T114.

---

## 2. Public semantic type

`TerminalPointerShape` exposes exactly 30 values:

```text
Alias
Cell
Copy
Crosshair
Default
EastResize
EastWestResize
Grab
Grabbing
Help
Move
NorthResize
NorthEastResize
NorthEastSouthWestResize
NoDrop
NotAllowed
NorthSouthResize
NorthWestResize
NorthWestSouthEastResize
Pointer
Progress
SouthResize
SouthEastResize
SouthWestResize
Text
VerticalText
WestResize
Wait
ZoomIn
ZoomOut
```

These are terminal mouse-pointer shapes. They are deliberately independent from text-cursor shape and cursor visibility.

---

## 3. Canonical wire mapping

The internal `TerminalPointerShapeCodec` maps each public semantic value to exactly one canonical OSC 22 name and parses those names back to the semantic value.

Examples:

```text
Pointer                    -> pointer
EastWestResize             -> ew-resize
NorthEastSouthWestResize   -> nesw-resize
VerticalText               -> vertical-text
ZoomIn                     -> zoom-in
```

No aliases are accepted.

---

## 4. Strict reverse parsing

Reverse parsing is intentionally strict so later Kitty OSC 22 query responses cannot silently broaden the frozen public contract.

Rejected input includes:

- empty string;
- casing variants such as `Pointer`;
- X11 names such as `left_ptr` and `hand2`;
- comma-separated lists;
- Kitty push/pop/query syntax;
- pseudo query names such as `__current__`.

Unknown names produce `FormatException`; undefined enum values produce `ArgumentOutOfRangeException`.

---

## 5. `Default` remains distinct from reset

`TerminalPointerShape.Default` maps to the actual canonical wire name:

```text
default
```

It is not equivalent to the empty OSC 22 terminal-policy reset frame.

This distinction is asserted directly in the T112 tests.

---

## 6. Query-readiness without query coupling

The reverse codec is implemented in T112 even though ordinary set/lease paths need only forward encoding.

This is deliberate: T114 can reuse the same frozen semantic decoder for explicit Kitty-compatible query responses rather than introducing a second name parser later.

No automatic query behavior is introduced by this tranche.

---

## 7. Tests

`TerminalPointerShapeTests` proves:

- exactly 30 public values exist;
- every value maps to the expected canonical name;
- every canonical name round-trips back to the same semantic value;
- `Default` differs from terminal-policy reset;
- noncanonical aliases/casing/lists/query syntax are rejected;
- null parsing and undefined enum values fail explicitly.

---

## 8. T112 decision

The semantic pointer vocabulary and canonical codec are now frozen for 0.11.

T113 may build session-managed ordered pointer-shape ownership on this semantic layer without exposing raw OSC 22 strings or depending on terminal-side push/pop stacks.
