# N157 — Deterministic Semantic Backend Routing Policy

## Status

Implemented and validated on the Icod.Terminal 1.5.0 development branch.

N157 defines how `Icod.Terminal` chooses among the reviewed backend candidates registered by N156. It does not add a public auto-routing API and it does not change the wire behavior of any existing public 1.x protocol-specific method.

## Separation of concerns

N157 preserves four independent concepts:

```text
semantic operation
    -> N156 candidate registry
    -> N155 capability evidence
    -> N157 routing preference
    -> selected backend
```

Registry declaration order is not routing preference. Capability evidence is not caller preference. Terminal identity is not capability proof.

## Resolution order

Automatic routing evaluates one semantic operation in the following order:

1. endpoint availability;
2. verified live backend evidence;
3. exact TermInfo implementation evidence;
4. advertised backend evidence;
5. reviewed safe fallback for an otherwise unknown backend;
6. unresolved `Unknown` or aggregate `Unsupported`.

A known `Unsupported` backend is never selected automatically.

## Explicit backend selection

An explicit internal backend selection is treated as routing policy, not capability evidence.

- the requested backend must be a reviewed N156 candidate for the semantic operation;
- `Verified`, `Advertised`, and `Unknown` may be explicitly selected;
- explicit selection does not override `Unsupported` or endpoint `Unavailable`;
- explicit selection never bypasses framing, validation, output serialization, security, or lifecycle requirements.

## Preference table

The initial reviewed preferences include:

```text
DesktopNotification
    OSC 99
    OSC 777
    OSC 9          [safe fallback]

CurrentLocation
    OSC 7          [safe fallback]
    OSC 9;9

ClipboardWrite
    exact TermInfo
    OSC 52
    [no blind fallback]

CursorStyle
    exact TermInfo
    DECSCUSR
    [no blind fallback]

SemanticPromptLifecycle
    OSC 133        [safe fallback]
    OSC 633

RasterGraphics
    Kitty Graphics
    Sixel
    [no blind fallback]
```

Other semantic operations have explicit deterministic policy entries as well. The resolver does not infer preference from enum order or registry order.

## TermInfo handling

`TermInfoCapability` is a semantic implementation candidate rather than a raw control family. It is selected only when N155 contains `TermInfo` advertisement for that semantic operation. A general semantic observation from another source does not masquerade as proof that a TermInfo recipe exists.

## Aggregate support result

When no candidate is selected:

- all reviewed candidates explicitly `Unsupported` -> semantic result `Unsupported`;
- otherwise -> semantic result `Unknown`;
- unavailable endpoints -> semantic result `Unavailable`.

## Safety rules

N157 does not:

- mutate existing wire-specific APIs;
- emit a protocol merely because a terminal brand appears to support it;
- convert query timeout into `Unsupported`;
- introduce a generic raw control writer;
- let caller preference become capability evidence;
- let a safe fallback override explicit negative evidence.

## Acceptance evidence

The N157 test suite covers:

- verified evidence overriding preference order;
- verified protocol evidence outranking TermInfo advertisement;
- exact TermInfo advertisement outranking weaker profile advertisement;
- advertised candidate preference;
- reviewed safe fallback only for approved backends;
- no blind fallback for clipboard, cursor style, or raster graphics;
- Kitty Graphics versus Sixel preference;
- explicit backend selection and rejection of unsupported/unregistered selections;
- aggregate `Unsupported` versus `Unknown`;
- endpoint `Unavailable`;
- separation of semantic evidence from exact TermInfo implementation evidence.

The exact N157 implementation head `1091a508181295ecbdf3bff591948e251c307dac` passed the full PR Staging workflow, including Windows, Linux, macOS, package candidate, all package-contract shards, and validated package artifact.
