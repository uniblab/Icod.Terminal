# N158 — Existing Protocol and TermInfo Reconciliation

## Status

Implemented on the Icod.Terminal 1.5.0 development branch; exact-head validation remains the acceptance gate before N159 closure.

N158 reconciles the existing 1.0–1.4 protocol surface with the N155 capability-evidence ledger, N156 semantic backend registry, and N157 deterministic resolver. It does not reinterpret any released public wire-specific method and does not add a public automatic-routing API.

## Ownership boundary

The normalized flow remains:

```text
Icod.DCurses / application
    semantic intent
        |
        v
Icod.Terminal
    semantic registry
    capability evidence
    routing policy
        |
        +--- Icod.TermInfo selected immutable profile
        +--- live protocol evidence
        v
reviewed wire backend
```

`Icod.TermInfo` describes immutable static capabilities and exact terminal recipes. `Icod.Terminal` owns live probing, evidence lifetime, backend resolution, wire I/O, and lifecycle invalidation.

## Reconciliation matrix

| Semantic intent | Existing backend(s) | TermInfo relationship | N158 result |
| --- | --- | --- | --- |
| Terminal title | OSC 0 / OSC 2 | no exact reviewed TermInfo equivalent | keep explicit OSC backends |
| Desktop notification | OSC 9 / OSC 777 / OSC 99 | no exact TermInfo equivalent | semantic alternatives; N157 policy chooses only from evidence/safe fallback |
| Current location | OSC 7 / OSC 9;9 | no exact TermInfo equivalent | OSC 7 remains portable semantic location; OSC 9;9 remains Windows compatibility |
| Shell current-directory metadata | OSC 633 `Cwd` / OSC 1337 `CurrentDir` | no exact TermInfo equivalent | metadata companions, not aliases of portable OSC 7 |
| Hyperlink | OSC 8 | no exact TermInfo equivalent | one reviewed backend |
| Clipboard write | OSC 52 | extended `Ms` is an exact parameterized write recipe | advertise `TermInfoCapability` for `ClipboardWrite` when `Ms` is present |
| Clipboard read | OSC 52 query | `Ms` is write-only and does not imply observation | OSC 52 remains the only current read backend |
| Cursor style | DECSCUSR | extended `Ss` is an exact set-style recipe | advertise `TermInfoCapability` for `CursorStyle` when `Ss` is present |
| Cursor-style observation | DECRQSS | `Ss`/`Se` do not provide live observation | DECRQSS remains separate |
| Palette mutation | OSC 4 | `can_change_color` + `initc` is an exact palette mutation contract | advertise `TermInfoCapability` for `PaletteColor` only when both are present |
| Dynamic terminal resources | OSC 10–14 / 17 / 19 | `Cs`/`Cr` cover only text-cursor color | do not promote partial cursor-color metadata to the whole `DynamicColor` semantic family |
| Bracketed paste | DEC private mode 2004 | `BE`/`BD` plus `PS`/`PE` form the existing complete reversible contract | advertise the existing CSI bracketed-paste backend from TermInfo evidence |
| Focus reporting | DEC private mode 1004 | `fe`/`fd` plus `kxIN`/`kxOUT` form the existing complete reversible contract | advertise the existing CSI focus backend from TermInfo evidence |
| Mouse reporting | DEC tracking modes / SGR or legacy encoding | `XM`/`xm` plus `kmous`/`KeyMouse` describes the existing supported input protocol | advertise the existing CSI mouse backend from TermInfo evidence |
| Synchronized output | DEC private mode 2026 | no reviewed exact TermInfo recipe in the current selected model | keep CSI backend explicit |
| Progress | OSC 9;4 | no exact TermInfo equivalent | keep OSC backend explicit |
| Pointer shape | OSC 22 | no exact TermInfo equivalent | keep OSC backend explicit |
| Semantic prompt lifecycle | OSC 133 / OSC 633 | no exact TermInfo equivalent | keep distinct portable/vendor backend coverage |
| Shell integration metadata | OSC 633 / OSC 1337 | no exact TermInfo equivalent | keep vendor metadata backends distinct |
| Keyboard reporting | Kitty CSI / modifyOtherKeys | selected profile metadata describes traditional/extended key decoding but does not prove a modern negotiated keyboard backend | no broad TermInfo promotion in N158 |
| DA / DSR / CPR | CSI query families | observation protocols, not presentation alternatives | remain query/observation backends |
| DECRQSS | DCS query | observation protocol | remains independent |
| XTGETTCAP | DCS query | live observation of specific capability names | results stay capability-name-specific; no broad semantic backend claim |
| Raster graphics | future Sixel / Kitty Graphics | no current exact TermInfo routing contract frozen by 1.5 | reserved for 1.7/1.8 live/static evidence work |

## Exact TermInfo implementation rules

N158 treats a TermInfo capability as an exact semantic backend only when the complete reviewed contract is present.

### Clipboard write

```text
Ms
```

`Ms` is a parameterized clipboard/selection mutation recipe. Its presence advertises the semantic `ClipboardWrite` TermInfo candidate. It does not imply clipboard read support.

### Cursor style

```text
Ss
```

`Ss` is an exact cursor-style set recipe. `Se` remains useful reset metadata but is not required to establish the ability to set an explicit style. Cursor-style observation still requires DECRQSS.

### Palette mutation

```text
can_change_color
initc
```

Both are required. A direct-color profile without palette mutation therefore does not gain the TermInfo palette candidate merely because it supports ordinary foreground/background rendition.

## Existing CSI input metadata

N158 does not relabel focus, paste, or mouse as the generic `TermInfoCapability` backend because the implementation already has explicit CSI semantic backends. Instead, the selected TermInfo profile supplies static `Advertised` evidence for those concrete backends.

Complete focus contract:

```text
fe
fd
kxIN
kxOUT
```

Complete bracketed-paste contract:

```text
BE
BD
PS
PE
```

Mouse contract:

```text
XM
xm
KeyMouse/kmous prefix recognized as SGR or legacy mouse input
```

Partial metadata is not capability evidence.

## Partial overlap is not equivalence

The xterm-family extended capabilities:

```text
Cs
Cr
```

set/reset text-cursor color. The existing `TerminalDynamicColor` family includes default foreground/background, text cursor, mouse foreground/background, and highlight foreground/background resources. N158 therefore does not use `Cs`/`Cr` as evidence for the entire `DynamicColor` semantic operation.

If later routing needs a TermInfo cursor-color backend, the semantic model should first split that narrower operation explicitly rather than overclaiming the broad dynamic-color family.

## Live evidence integration

A successful, correlated and successfully parsed Kitty OSC 99 support response records:

```text
backend: Osc99KittyNotification
state: Verified
source: ProtocolResponse
```

A timeout or cancellation records nothing. This preserves the rule:

```text
timeout != unsupported
```

The existing public query result and exceptions remain unchanged. A session-level regression test drives the real `QueryKittyNotificationSupportAsync(...)` request/response path and proves that the successful response upgrades desktop-notification routing to OSC 99.

## Live-evidence generation and invalidation

Static TermInfo/profile evidence survives state invalidation because the selected `TerminalDescription` is immutable for the session.

Live probe/response evidence is generation-scoped. `TerminalSession.InvalidateState()` advances the live-evidence generation before invalidating input/presentation state assumptions. This means an explicit out-of-band invalidation immediately discards stale live capability conclusions while preserving static TermInfo/profile evidence.

Managed lifecycle resume already calls `InvalidateState()` before session state is reapplied, so the same rule covers suspend/resume without introducing a second lifecycle-specific evidence path.

The resulting invariant is:

```text
InvalidateState()
    -> discard live probe/protocol-response evidence
    -> retain immutable TermInfo/profile advertisement
    -> require later probes to repopulate live proof
```

## Session integration

`TerminalSession` now owns one lazily initialized semantic-evidence ledger. The first resolution seeds static evidence from the selected `TerminalDescription`. Internal live probes may record reviewed backend evidence into the same ledger.

Endpoint availability is also semantic:

- active query/input semantics require interactive input and output;
- output-only semantic operations require an interactive output endpoint;
- endpoint unavailability remains an effective routing state, not stored capability evidence.

The session tests prove that `InvalidateState()` removes a verified live choice and restores the surviving exact TermInfo choice when one exists. The OSC 99 integration test similarly proves that invalidation removes a verified Kitty notification choice and returns routing to the reviewed OSC 9 unknown-support fallback.

## Compatibility

N158 does not:

- change existing public method signatures;
- change the bytes emitted by an existing explicit API;
- make `SendNotificationAsync(...)` choose OSC 777 or OSC 99;
- make OSC 7 emit OSC 633/1337 companions;
- make `SetCursorStyleAsync(...)` silently switch to TermInfo;
- make current clipboard APIs silently switch backends;
- infer support from terminal brand alone;
- expose a generic protocol writer.

The new registry/evidence/resolver path remains internal until a future public semantic-routing API receives its own compatibility and security review.

## Acceptance

N158 is complete when:

1. exact TermInfo recipes are represented as static evidence without overclaiming partial overlaps;
2. existing focus/paste/mouse metadata feeds the corresponding reviewed CSI backend identities;
3. successful reviewed live probes can upgrade backend evidence;
4. `InvalidateState()` invalidates live evidence while immutable TermInfo/profile evidence persists;
5. existing 1.0–1.4 public protocol-specific APIs retain their released behavior;
6. the full Staging runtime/package matrix remains green.
