# T33 — OSC 2 Semantic Window-Title Operation

**Project:** `Icod.Terminal`  
**Release line:** `0.4.0`  
**Tranche:** T33  
**Development version:** `0.4.0-alpha.5`  
**Theme:** OSC 2 window-title-only semantic operation  
**Status:** Implemented

---

## 1. Purpose

T33 adds the OSC 2 semantic operation on top of the shared OSC writer established by T30 and the session-level title-operation policy introduced by T31/T32.

The public API is:

```csharp
await session.SetWindowTitleAsync(
	"window title"
);
```

The operation emits:

```text
ESC ] 2 ; Pt ESC \
```

where `Pt` is the strict UTF-8 payload validated by the shared internal writer.

---

## 2. Contract

`SetWindowTitleAsync(...)` SHALL:

- emit OSC selector 2 only;
- leave OSC 0 and OSC 1 semantics distinct;
- reuse `OscWriter` for framing, validation, UTF-8 encoding, and payload bounds;
- reject C0, DEL, C1, ill-formed Unicode, and payloads over 4096 encoded bytes;
- reject known non-terminal output endpoints;
- honor cancellation before transmission;
- remain emission-oriented rather than claiming terminal-side application;
- avoid adding any public raw OSC selector or escape-sequence API.

Successful completion means the complete OSC 2 frame was submitted to the session output. It does not prove that the terminal displayed or retained the requested window title.

---

## 3. API integration

The OSC 0, 1, and 2 methods now share one private session helper:

```text
SetTitleAsync(...)       -> OSC 0
SetIconNameAsync(...)    -> OSC 1
SetWindowTitleAsync(...) -> OSC 2
```

All three operations therefore use one endpoint policy and one internal writer path.

T33 does not duplicate protocol framing code.

---

## 4. Tests

Deterministic in-memory tests cover:

- empty OSC 2 payload;
- ASCII window title;
- multilingual strict UTF-8 payload;
- invalid/control payload rejection with zero output;
- redirected/non-terminal output rejection with zero output;
- cancellation before transmission with zero output.

The tests do not change the CI runner's real terminal title.

---

## 5. Non-goals

T33 does not add:

- OSC 7;
- OSC 8;
- OSC 52;
- title querying;
- title stack push/pop;
- title restoration;
- a generic public OSC API;
- cursor-style operations;
- synchronized output.

---

## 6. Completion gate

T33 is complete when:

1. `TerminalSession.SetWindowTitleAsync(...)` emits byte-exact OSC 2;
2. OSC 0, OSC 1, and OSC 2 remain distinct semantic public operations;
3. the three methods share the T30 writer and common session helper;
4. T29 safety/resource rules remain unchanged;
5. deterministic tests validate the new operation;
6. package metadata advances to `0.4.0-alpha.5` while assembly version remains `0.4.0.0`;
7. the cross-platform Staging PR gate is green.

After T33, the complete OSC 0/1/2 semantic title family exists. T34 can therefore concentrate on output ordering, concurrency, session state, and flush semantics rather than adding another selector.
