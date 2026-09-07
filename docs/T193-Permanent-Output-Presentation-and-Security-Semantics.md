# T193 — Permanent Output, Presentation, and Security Semantics

**Release:** `Icod.Terminal 1.0.0-rc1`  
**Predecessor:** T192 — permanent input/query semantics, workflow #924  
**Status:** Implementation complete; exact-head validation pending

## 1. Purpose

T193 consolidates the accumulated semantic-output, presentation, reversible-state, security, and privacy contracts into permanent 1.x authorities.

No new protocol, public API, or wire form is introduced.

## 2. Permanent authorities

T193 adds:

- `docs/Presentation-and-Reversible-State.md`;
- `docs/Semantic-Output-Protocols.md`;
- `docs/Security-and-Privacy.md`.

These documents replace the need to reconstruct current behavior from the 0.4–0.16 release roadmaps and tranche records.

## 3. Four state categories

The audit found that a generic “terminal lease/reset” description would be materially misleading. T193 therefore permanently distinguishes four categories:

1. **exact-restoration state** — an external baseline is observed/captured and replayed exactly;
2. **terminal-policy reset state** — the protocol returns control to terminal policy without claiming the exact prior value;
3. **library-owned nested state without an observable external baseline** — outer Icod-owned state can be restored, but unknown pre-Icod state is not claimed;
4. **ephemeral metadata** — explicit output with no retained/restorable lifecycle state.

Examples are documented centrally so future APIs cannot claim stronger restoration merely for naming symmetry.

## 4. Presentation contract consolidated

`Presentation-and-Reversible-State.md` documents:

- alternate screen, keypad mode, and cursor visibility lease composition;
- overlapping lease rules;
- newest cursor-visibility request precedence;
- transactional presentation transitions and rollback failure;
- lifecycle/teardown acquisition barriers;
- composition with screen-local modern keyboard state;
- cursor visibility vs cursor style vs pointer shape;
- exact cursor-style baseline restoration;
- synchronized-output first/last-owner behavior;
- progress ownership and logical/physical separation;
- pointer terminal-policy reset vs exact restoration;
- palette/dynamic exact-restoration leases;
- reset-vs-restoration distinction for OSC 104/110–119;
- hyperlink LIFO ownership;
- invalidation and final session disposal authority.

## 5. Semantic output inventory consolidated

`Semantic-Output-Protocols.md` documents the supported semantic families in one place:

- application text and advanced terminfo output;
- OSC 0/1/2 titles;
- OSC 7 current location;
- OSC 8 hyperlinks;
- OSC 52 clipboard/selections;
- DECSCUSR cursor style;
- DEC mode 2026 synchronized output;
- OSC 9;4 progress;
- OSC 22 pointer shape;
- OSC 133 prompt/command metadata;
- safe OSC 9 notification and 9;9 compatibility;
- OSC 4/104 palette colors;
- dynamic OSC 10–14, 17, 19 and their reset forms.

The document retains payload/resource bounds and explains emission-vs-support semantics without encouraging raw protocol construction.

## 6. Safe OSC 9 boundary

T193 makes the retained OSC 9 boundary a permanent 1.x security decision.

Supported semantic forms remain:

- legacy notification;
- OSC 9;4 progress;
- explicit Windows current-directory compatibility through OSC 9;9.

Hazardous host-affecting/vendor operations remain excluded, including sleep, GUI message boxes, wait-for-key, GUI macros, process launch, environment disclosure, and xterm/emulation mutation.

Redundant title/prompt subcommands remain excluded because existing semantic OSC 0/1/2 and OSC 133 APIs own those concepts.

No generic public OSC 9 dispatcher is introduced.

## 7. Security/privacy contract consolidated

`Security-and-Privacy.md` now centralizes:

- terminal control-sequence injection boundaries;
- bounded parser/query resource rules;
- the one-authoritative-reader integrity boundary;
- no terminal-brand support oracle;
- emission vs application semantics;
- clipboard read/write privacy;
- explicit current-location disclosure;
- hyperlink scheme trust/application policy;
- OSC 133 command-line disclosure and no automatic redaction;
- notification history/lock-screen disclosure;
- modern keyboard metadata privacy;
- focus/mouse/paste data sensitivity;
- environment fingerprinting through explicit queries;
- redirected-output truthfulness;
- advanced raw-output hazards;
- exact restoration and state uncertainty;
- suspend/resume observation trust boundaries;
- no hidden PTY/process/network/browser/OS-clipboard side effects.

## 8. Advanced output boundary

T193 preserves the T190 decision that `TerminalSession.Output` remains public as an advanced borrowed transport.

The permanent documentation now states clearly that direct raw output:

- is outside session serialization;
- bypasses semantic injection/resource validation;
- can interleave with query/lifecycle/state traffic;
- is caller responsibility.

`WriteTerminalStringAsync(...)` is likewise documented as an already-resolved terminfo/padding boundary, not a recommended generic escape-sequence API.

## 9. Historical records

Historical release records remain useful evidence for protocol references, byte-exact fixtures, and design decisions. In particular, the 0.4–0.16 public API baselines and corresponding T-series records remain preserved.

Consumers should use the permanent T191–T193 authorities for the current 1.x contract rather than reconstructing it from those historical milestones.

## 10. Exit criteria

T193 closes when:

1. the three permanent authorities exist and agree with the current public behavior;
2. restoration categories are described without conflating reset with exact restoration;
3. every semantic output family through 0.16 is represented permanently;
4. safe OSC 9 exclusions are explicitly permanent;
5. metadata disclosure/privacy boundaries are consolidated;
6. the advanced raw-output boundary is stated explicitly;
7. no runtime feature/API/protocol change has entered T193;
8. the exact T193 documentation head passes Windows/Linux/macOS, real DCurses acceptance/soak, exact package verification, and all retained 0.8–0.18 package contracts.
