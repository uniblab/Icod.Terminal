# T110 — OSC 22 Pointer-Shape Contract and Reference Freeze

**Release:** `0.11.0`  
**Tranche:** `T110`  
**Development version:** `0.11.0-alpha.1`  
**Status:** Frozen

---

## 1. Purpose

T110 freezes the protocol, vocabulary, ownership, lifecycle, compatibility, and public-abstraction rules for `Icod.Terminal 0.11.0` before OSC 22 implementation begins.

OSC 22 controls the terminal **mouse pointer** shape. It is independent of DECSCUSR text-cursor style (`TerminalCursorStyle`) and independent of cursor visibility (`TerminalCursorVisibility`).

The release SHALL expose semantic pointer operations rather than a public arbitrary OSC 22 string writer.

---

## 2. Reference model

### xterm base behavior

xterm defines OSC 22 as changing the pointer cursor shape to a string value. An empty value, or a value not matching xterm's accepted pointer names, returns xterm to its configured default pointer shape.

The xterm vocabulary is historically tied to X11 cursor names and therefore is not a platform-independent public abstraction for `Icod.Terminal`.

### Kitty extension

Kitty adopts the same basic OSC 22 setter form while defining a portable CSS-derived pointer-shape vocabulary. Kitty additionally defines:

- `>` push operations;
- `<` pop operations;
- `?` support/current/default/grabbed queries;
- separate pointer-shape stacks for main and alternate screens.

Those additions are extensions to the xterm-compatible basic setter/reset behavior.

### Modern compatibility posture

Ghostty and Foot follow the CSS-style vocabulary; Kitty defines the same portable list. Other terminals may implement OSC 22 using X11-derived names, a subset, another naming system, or no OSC 22 support at all.

`Icod.Terminal` SHALL therefore use CSS semantic names as its canonical public vocabulary while retaining truthful optimistic-support semantics.

---

## 3. Canonical wire framing

The canonical outbound base setter SHALL be seven-bit OSC with explicit String Terminator:

```text
ESC ] 2 2 ; shape ESC \
```

For example:

```text
ESC ] 2 2 ; pointer ESC \
ESC ] 2 2 ; text ESC \
ESC ] 2 2 ; wait ESC \
```

The canonical terminal-policy reset SHALL be:

```text
ESC ] 2 2 ; ESC \
```

T111 SHALL construct each complete frame before commit and emit it through one non-caller-cancellable transport write after caller cancellation has been observed before commit.

No OSC 22 operation implicitly flushes output.

BEL termination SHALL NOT be the canonical outbound representation for 0.11. The explicit ST form is chosen because it is the documented Kitty form and is valid OSC framing for xterm-family terminals.

---

## 4. Public semantic pointer vocabulary

`0.11.0` SHALL expose the complete 30-name CSS-compatible set required by Kitty-compatible OSC 22 implementations.

The semantic values and canonical wire names are:

| Public semantic value | Canonical wire name |
| --- | --- |
| `Alias` | `alias` |
| `Cell` | `cell` |
| `Copy` | `copy` |
| `Crosshair` | `crosshair` |
| `Default` | `default` |
| `EastResize` | `e-resize` |
| `EastWestResize` | `ew-resize` |
| `Grab` | `grab` |
| `Grabbing` | `grabbing` |
| `Help` | `help` |
| `Move` | `move` |
| `NorthResize` | `n-resize` |
| `NorthEastResize` | `ne-resize` |
| `NorthEastSouthWestResize` | `nesw-resize` |
| `NoDrop` | `no-drop` |
| `NotAllowed` | `not-allowed` |
| `NorthSouthResize` | `ns-resize` |
| `NorthWestResize` | `nw-resize` |
| `NorthWestSouthEastResize` | `nwse-resize` |
| `Pointer` | `pointer` |
| `Progress` | `progress` |
| `SouthResize` | `s-resize` |
| `SouthEastResize` | `se-resize` |
| `SouthWestResize` | `sw-resize` |
| `Text` | `text` |
| `VerticalText` | `vertical-text` |
| `WestResize` | `w-resize` |
| `Wait` | `wait` |
| `ZoomIn` | `zoom-in` |
| `ZoomOut` | `zoom-out` |

No public arbitrary pointer-name string is added in 0.11.

---

## 5. `Default` shape versus terminal-policy reset

These operations are distinct and SHALL remain distinct in the public abstraction.

`TerminalPointerShape.Default` requests the CSS pointer shape named:

```text
default
```

and therefore emits:

```text
ESC ] 2 2 ; default ESC \
```

A terminal-policy reset releases the application's explicit pointer-shape request and emits an empty OSC 22 payload:

```text
ESC ] 2 2 ; ESC \
```

Lease final release and managed suspension use terminal-policy reset, not `TerminalPointerShape.Default`.

---

## 6. Truthful support posture

Successful completion of an OSC 22 operation proves only that `Icod.Terminal` emitted the complete requested frame.

It does not prove that the attached terminal:

- supports OSC 22;
- recognizes the requested CSS name;
- visually applies the requested shape;
- continues to honor the requested shape while selecting text, hovering links, or performing other terminal-owned mouse interactions.

`Icod.Terminal` SHALL NOT infer OSC 22 support from:

- operating system;
- `TERM`;
- terminal-emulator environment variables;
- terminal identity strings;
- platform kind.

Ordinary setter or lease acquisition SHALL NOT perform an automatic query.

---

## 7. Explicit setter and reset

0.11 SHALL provide an explicit semantic setter for callers which do not need scoped restoration:

```csharp
await session.SetPointerShapeAsync(
	TerminalPointerShape.Pointer
);
```

and an explicit terminal-policy reset:

```csharp
await session.ResetPointerShapeAsync();
```

These explicit operations participate in session-owned output ordering but do not themselves create a scoped logical owner.

If scoped pointer-shape ownership is active, explicit pointer-shape setters SHALL be rejected rather than silently corrupting the manager's physical-state assumptions. This rule is preferable to implicit invalidation because the same session already owns the conflicting state.

Out-of-band raw writes performed outside the semantic API remain caller responsibility; `TerminalSession.InvalidateState()` is the recovery mechanism for such external mutation.

---

## 8. Scoped ownership model

0.11 SHALL provide session-managed scoped pointer-shape ownership.

Expected acquisition form:

```csharp
await using TerminalPointerShapeLease pointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.Pointer
	);
```

### First owner

The first owner emits its requested shape after ownership has been established at the logical commit boundary.

Because portable xterm OSC 22 provides no query for the pre-existing external pointer shape, the library SHALL NOT claim exact restoration of arbitrary pre-lease terminal state.

### Nested owners

Nested owners are ordered by acquisition.

The most recently acquired active owner controls physical pointer shape.

When the controlling owner releases, the newest remaining owner is restored.

### Out-of-order release

Disposing a non-controlling owner is logical-only and emits no pointer frame.

Disposing the controlling owner restores the newest remaining owner, even when lower owners were previously released out of order.

### Final release

Final release emits terminal-policy reset:

```text
ESC ] 2 2 ; ESC \
```

It SHALL NOT guess or claim restoration of an unknown external pointer shape.

### Terminal-side stack

The core lease implementation SHALL NOT depend on Kitty's `>`/`<` terminal-side pointer stack.

Library-managed ownership emits ordinary set/reset frames so the same public lease semantics remain usable on terminals implementing only the basic xterm-compatible OSC 22 setter.

---

## 9. Cancellation and commit boundary

Setter/acquisition semantics SHALL follow the established terminal-control commit model:

1. validate semantic input;
2. acquire the appropriate session output serialization lease using the caller token;
3. construct the complete OSC 22 frame;
4. observe caller cancellation before commit;
5. write the complete frame using `CancellationToken.None`;
6. update physical/logical state only according to the success/failure rules frozen for the operation.

Caller cancellation SHALL NOT deliberately truncate a committed OSC 22 frame.

Cleanup/restoration operations are non-caller-cancellable.

---

## 10. Failure and cleanup debt

A failed physical pointer-shape transition means terminal state may be uncertain.

Scoped ownership SHALL retain cleanup/recovery responsibility rather than pretending the requested/restored shape is known to be active.

Rules:

- failed acquisition before physical commit establishes no lease;
- failed nested transition does not silently transfer physical-control certainty;
- failed controlling release retains that owner's cleanup responsibility so the same lease may retry;
- cleanup debt blocks new scoped acquisitions until recovered;
- session disposal remains authoritative best-effort reset;
- double failures during re-entry plus cleanup are aggregated rather than losing either error.

T115 SHALL lock these rules with failure-injection tests.

---

## 11. Lifecycle semantics

### Suspend

Before managed terminal suspension, an active library-owned pointer shape is reset to terminal policy:

```text
ESC ] 2 2 ; ESC \
```

Logical owners remain alive.

### Release while suspended

Lease releases while suspended update logical ownership only and emit nothing.

If all owners release while suspended, resume emits no OSC 22 shape.

### Resume

If a logical owner remains after suspension, resume re-applies the current controlling semantic shape.

### Invalidation

`TerminalSession.InvalidateState()` marks pointer physical-state assumptions untrusted.

The next controlled transition SHALL re-establish the current logical state before continuing, or reset when no logical owner remains.

### Session disposal

Session disposal performs authoritative pointer-policy reset before closing terminal output state.

Late successful lease disposal after session cleanup emits nothing.

---

## 12. Interaction with alternate screen

Kitty specifies separate pointer stacks for main and alternate screen, but the portable base OSC 22 setter has no corresponding stack contract.

`Icod.Terminal` therefore SHALL NOT pretend that base OSC 22 pointer state is automatically screen-local.

The session-managed pointer owner remains logically active across presentation-state transitions. T116 SHALL verify that pointer-shape operations compose with alternate-screen/presentation leases without deadlock or output-gate lifetime coupling.

No special terminal-side push/pop is inserted merely because an alternate-screen lease is active.

---

## 13. Interaction with mouse reporting and hyperlinks

Pointer shape is independent of terminal mouse reporting protocols.

A caller may request a pointer shape whether or not mouse reporting is enabled.

The terminal remains free to display a different pointer while:

- selecting text;
- dragging;
- hovering a URL;
- hovering an OSC 8 hyperlink;
- performing terminal-owned UI interactions.

The library does not attempt to suppress those terminal behaviors.

---

## 14. Explicit query extension

Kitty defines explicit OSC 22 queries using `?`, including:

```text
?__current__
?__default__
?__grabbed__
?name1,name2,...
```

with OSC 22 responses.

0.11 SHALL treat this as an **explicit optional query extension**, not part of ordinary setter/acquisition semantics.

The planned public query surface MAY include:

- current pointer-shape observation;
- terminal default pointer-shape observation;
- grabbed pointer-shape observation;
- support queries for one or more semantic shapes.

Requirements:

- query calls are explicit;
- queries use the existing active-query/router architecture;
- queries have caller-visible finite deadlines;
- timeout is not treated as proof of unsupported OSC 22;
- malformed responses are reported as malformed rather than guessed;
- no query occurs during ordinary `SetPointerShapeAsync(...)` or `AcquirePointerShapeAsync(...)`.

T114 may narrow the exact public query surface after T111–T113 establish the base setter/ownership path, but SHALL NOT broaden it beyond Kitty's documented query semantics.

---

## 15. Terminal-side push/pop extension disposition

Kitty's `>` push and `<` pop operations are useful protocol features but are not required for portable session-managed leasing.

0.11 SHALL NOT expose raw push/pop operations in the initial public surface.

If later tranches find a compelling public need, they may be added only as typed semantic operations with tests proving they do not conflict with library-managed ownership. They are not required for 0.11 stable closure.

---

## 16. Composition requirements

OSC 22 pointer operations SHALL compose with:

- ordinary text writes;
- OSC 0/1/2 title operations;
- OSC 7 location publication;
- OSC 8 hyperlinks;
- OSC 9;4 progress;
- OSC 52 clipboard;
- DECSCUSR cursor style;
- presentation state;
- synchronized output;
- input protocol leases;
- active terminal queries.

Pointer ownership SHALL NOT hold the session output gate for the lifetime of a lease.

---

## 17. Downstream use case

A higher-level UI consumer such as `Icod.DCurses` should be able to express pointer semantics such as:

```csharp
await using TerminalPointerShapeLease linkPointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.Pointer
	);
```

or resize semantics such as:

```csharp
await using TerminalPointerShapeLease resizePointer =
	await session.AcquirePointerShapeAsync(
		TerminalPointerShape.EastWestResize
	);
```

without constructing OSC bytes or terminal-specific cursor names.

T116 SHALL provide real downstream acceptance.

---

## 18. Explicit non-goals

0.11 SHALL NOT add:

- public arbitrary OSC 22 strings;
- generic public OSC construction;
- an executable/window/Dock icon API;
- DECSCUSR text-cursor semantics as part of pointer shape;
- native GUI mouse-pointer APIs;
- automatic terminal detection;
- automatic support queries on acquisition;
- arbitrary X11 cursor-name injection;
- application-side pointer rendering;
- a requirement that terminals implement Kitty's stack/query extensions in order to use the base semantic setter/lease.

---

## 19. T110 decision

The 0.11 architecture is frozen around:

1. the 30 CSS-compatible semantic pointer shapes;
2. canonical `OSC 22 ; shape ST` set frames;
3. canonical empty-payload terminal-policy reset;
4. explicit distinction between CSS `default` and terminal-policy reset;
5. semantic explicit set/reset APIs;
6. library-managed scoped nesting independent of Kitty's terminal-side stack;
7. truthful final reset rather than fabricated restoration of unknown external state;
8. lifecycle/invalidation/failure cleanup consistent with existing owned terminal state;
9. optional explicit Kitty query support through the existing query router;
10. no raw pointer strings or automatic capability inference.

T111 may now implement the byte-exact base OSC 22 writer.
