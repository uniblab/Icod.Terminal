# T190 — Public API Regret Audit

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Base:** published `0.18.0` at `c0f0ff482c8bb0d358c4fcb38e454d717b1b6f1c`  
**Baseline validation:** workflow #908  
**Correction validation:** workflow #911  
**Status:** Complete

## 1. Purpose

This record applies the T190 A–E decision classes to the accumulated pre-1.0 public surface. It is deliberately stricter than a documentation review: the question is whether each contract should be supported throughout 1.x, not whether it can merely be described.

The audit starts from the 0.18 behavioral baseline and the historical public API baselines, but current 1.0-facing semantics take precedence over early pre-1.0 shapes where those shapes evolved deliberately before the contract freeze.

## 2. Classified findings

| Area | Class | Decision |
| --- | --- | --- |
| `ITerminalInput` / `ITerminalOutput` | A | Freeze as public custom-transport injection seams. |
| `ITerminalControlProvider` and native snapshot/result contracts | A | Freeze as the deliberate low-level provider/diagnostic layer below normal semantic session policy. |
| `TerminalSession.Input` raw transport property | D | Removed from the public 1.0 surface. A session owns one authoritative input reader/decoder/query-router path; public raw reads can steal bytes and invalidate that contract. |
| `TerminalSession.Output` raw transport property | A/B | Retain as an explicit advanced escape hatch; XML/permanent docs state that direct use is outside session output serialization and requires caller responsibility. |
| `TerminalSession.OpenAsync(... ITerminalInput, ITerminalOutput ...)` | A | Freeze. Custom transport injection remains necessary even though the session no longer returns its owned input transport publicly. |
| `TerminalSession.ReadEventAsync(...)` cancellation-as-event semantics | A/B | Freeze behavior; permanent documentation must explain why caller cancellation does not cancel the underlying read and why `TerminalEventKind.Cancelled` is returned. |
| Lifecycle participant API | A/B | Freeze; permanent docs must define participant ordering, query restrictions during callbacks, ownership, and disposal. Internal lifecycle source/signal seams remain internal. |
| Typed query/result APIs | A | Freeze typed semantic contracts; parser/wire correlation machinery remains internal. |
| Query timeout, late-response ownership, and ambiguity rules | A/B | Freeze behavior established through 0.18; consolidate into permanent query documentation. |
| Presentation/rich-input/color leases | A/B | Freeze ownership model; consolidate first-owner/last-owner, invalidation, suspend/reentry, rollback, and exact-restoration semantics. |
| `WriteTerminalStringAsync(...)` | A/B | Retain as the low-level already-resolved terminfo string emission boundary needed by capability-driven renderers; document that it is not the preferred generic protocol API. |
| Semantic OSC/CSI/DEC operations | A | Freeze semantic bounded APIs; retain explicit exclusions on generic vendor dispatch/raw protocol builders. |
| Modern keyboard model | A | Freeze semantic phases/modifiers/keys and negotiated Kitty reporting; xterm `modifyOtherKeys` remains decode-only. |
| Current public enum numeric values | C | Freeze comprehensively before rc1 closure with an exact 1.0 public-surface/enum-value contract. Existing selective tests are not sufficient as the permanent 1.x baseline. |
| Historical release-oriented XML/README/sample prose | B | Rewrite or consolidate into permanent documentation during T191–T196. |
| `Icod.Terminal.csproj` release notes | B | Replace stale 0.16 notes with rc1/1.0 metadata during T196. |
| Original `Icod.Terminal-Development-Roadmap.md` | B | Reframe as historical or replace its stale current-state authority during permanent-document closure. |
| New protocols, arbitrary vendor expansion, PTY/process ownership | E | Defer; not required to support the frozen 1.0 contract. |

## 3. D-class decision — remove public `TerminalSession.Input`

### Existing 0.18 surface

0.18 exposed both:

```csharp
public ITerminalInput Input { get; }
public ITerminalOutput Output { get; }
```

The public `ITerminalInput` interface itself is intentional: callers can supply a custom transport when opening a session.

The regret lay in returning that input transport again from the live session.

### Concrete long-term regret

Once a `TerminalSession` is active, its decoder and query transaction manager require one authoritative reader over the input byte stream.

A caller using `session.Input.ReadAsync(...)` could consume bytes that belong to:

- a fragmented UTF-8 scalar;
- a fragmented escape/control sequence;
- an active correlated query response;
- a bracketed-paste frame;
- a modern keyboard frame;
- ordinary input waiting behind a query response.

The session cannot detect or repair such theft because the caller holds the raw borrowed transport directly. No internal lock can make arbitrary external reads safe.

Keeping the property in 1.0 would therefore publish an operation that can invalidate central parser/query guarantees while appearing to be an ordinary supported session capability.

### Why documentation is insufficient

A warning saying “do not read this property while the session is active” would leave a public member whose only safe general use contradicts its apparent purpose. The transport is already available to callers that supplied it; standard-session callers do not need a second reader at all.

### Compatibility impact

Code compiled against 0.x that reads `TerminalSession.Input` must change when moving to 1.0. This is a deliberate pre-1.0 breaking correction.

The audit found no Icod downstream application that reads from `TerminalSession.Input`. `Icod.DCurses` consumes `TerminalSession.ReadEventAsync(...)`, so the principal downstream path is already aligned with the corrected boundary.

### Migration

For terminal input owned by a session:

```csharp
TerminalEvent terminalEvent = await session.ReadEventAsync(...);
```

Use the typed query methods for query/response operations.

Callers that supplied a custom `ITerminalInput` may retain their original reference for lifetime management or use outside the session lifetime, but must not create a competing reader over the same transport while `TerminalSession` owns it.

### Why correcting before 1.0 is preferable to 2.0

The property undermines a core correctness invariant rather than representing a missing convenience. Freezing it for all of 1.x would either force support for unsafe mixed-reader behavior or require a major-version break immediately after 1.0. rc1 is the intended final opportunity to correct that abstraction boundary.

### Implementation and proof

`TerminalSession.Input` is now internal rather than public. The `ITerminalInput` interface and custom `OpenAsync(...)` injection overload remain public.

`TerminalSessionPublicSurfaceFreezeTests` verifies that:

- no public `TerminalSession.Input` property exists;
- `ITerminalInput` remains public;
- the custom `OpenAsync(...)` overload still accepts both transport interfaces;
- the intentionally retained raw output property remains visible.

Workflow #911 passed Windows/Linux/macOS, real DCurses acceptances and soak, exact package validation, and all retained 0.8–0.18 package contracts.

## 4. Why `TerminalSession.Output` is different

The output property is not removed by T190.

The historical public contract deliberately defines borrowed `session.Output` as outside session synchronization. Unlike raw input, there is no parser state or authoritative-reader cursor that a direct output operation consumes irreversibly. Advanced callers may choose to use the transport directly only when they accept responsibility for avoiding interleaving with session-managed output.

It also has an established downstream use: `Icod.DCurses` routes rendered text and terminfo strings through `TerminalSession` methods and uses the borrowed output service for flushing.

The 1.0 contract therefore retains `Output` and strengthens its XML documentation: direct use is not serialized with session-managed writes, query traffic, or lifecycle-owned control output. Ordinary consumers should prefer session APIs.

This is an intentional A/B decision rather than an accidental symmetry break.

## 5. Enum-value history and 1.0 freeze

Some early pre-1.0 enum layouts evolved before later releases began asserting numeric stability. For example, the original 0.1 `TerminalInputEventKind` contained `Text`, `Key`, and `EndOfInput`; rich-input kinds were inserted during 0.2-era development. The current contract, already tested by later releases, is:

```text
Text       = 0
Key        = 1
Mouse      = 2
Focus      = 3
Paste      = 4
EndOfInput = 5
```

T190 does not attempt to resurrect superseded 0.1 numeric layouts. The rc1 task is to freeze the **current** public values comprehensively and make that exact surface the 1.x compatibility baseline.

That is classified C and assigned to T194/package closure.

## 6. Options and live-policy mutation audit

`TerminalSession.Options` was reviewed as a possible mutable-policy escape hatch.

The public option properties are init-only. Nested `TerminalInputDecoderOptions` values are also init-only. The session clones `ApplicationEncoding` for live application-text use. Provider/clock/delay objects are explicit injected services rather than mutable scalar policy.

The audit therefore found no second D-class defect here. Permanent documentation should describe options as open-time policy, but no API correction is required.

## 7. Vendor/raw protocol leakage audit

The completed scan found no public generic OSC dispatcher, raw OSC 9/133 builder, public Kitty flag integer surface, or public raw Kitty private-use key-code surface.

The intentionally low-level `WriteTerminalStringAsync(...)` remains an already-resolved terminfo-string boundary for capability-driven renderers and is not reclassified as a general-purpose raw protocol API.

No D-class change is required.

## 8. Result/exception audit

The low-level control layer reuses `TerminalControlStatus.Available` for successful observation/mutation results. This is slightly unusual, but the typed result wrappers make success/availability behavior coherent and it is already deeply used by custom providers and package tests.

Unavailable, unsupported, controlled failure, thrown exceptional failure, cancellation, and query timeout have meaningful distinct roles. The audit found documentation work, not sufficient regret to justify a breaking result-model redesign before 1.0.

These semantics are assigned to T191/T192 permanent documentation.

## 9. No additional D-class changes

The completed T190 pass found no sufficient basis to redesign:

- `TerminalControlStatus` / mutation result reuse;
- `TerminalEndpoint` file-descriptor/path abstraction;
- native mode snapshots;
- lifecycle participants;
- `TerminalEventKind.Cancelled` semantics;
- typed query result shapes;
- semantic protocol APIs;
- presentation/input/color ownership leases;
- public modern-keyboard semantic types;
- `TerminalSession.Output` advanced transport access;
- open-time session option model.

Some of these APIs are low-level or unusual, but they have coherent use cases and can be documented/supportable throughout 1.x. Aesthetic preference is not a D-class reason.

## 10. Closure

T190 is complete.

The exact corrected head passed workflow #911. Permanent-document B-class work now moves to T191–T193/T195, comprehensive public-surface and enum-value C-class proof moves to T194, and package metadata cleanup moves to T196.

No new protocol implementation entered the release candidate.
