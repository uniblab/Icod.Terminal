# Kitty Graphics transaction compatibility — Icod.Terminal 1.28

This record separates the published Kitty Graphics Protocol, reviewed upstream
implementation behavior, deterministic transport tests, and operator-visible
rendering. It is not a terminal-brand support promise. Runtime routing uses
capability and transaction evidence, never a terminal name or version heuristic.

## Pinned authorities

- Published protocol source:
  [`docs/graphics-protocol.rst`](https://github.com/kovidgoyal/kitty/blob/96693f4c090e9477b51ffa46aed4abdcef52d037/docs/graphics-protocol.rst)
  at source commit `96693f4c090e9477b51ffa46aed4abdcef52d037`
  (document blob `1473d807edd602e50d6b6f8834967bf18964e996`).
- Implementation under qualification:
  [Kitty 0.49.2](https://github.com/kovidgoyal/kitty/releases/tag/v0.49.2).
- Upstream defect record:
  [kitty #10599](https://github.com/kovidgoyal/kitty/issues/10599).
- Post-0.49.2 correction:
  [`b493a637b0573997f00423e8454f91a966ac96de`](https://github.com/kovidgoyal/kitty/commit/b493a637b0573997f00423e8454f91a966ac96de).

The pinned protocol says animation-frame continuation chunks carry `a=f`, `m`,
and optional `q`; they do not repeat the image id. A nonzero image id on the
initial data command requests a response after transfer. Quietness `q=1`
suppresses success and `q=2` also suppresses errors. The text defines animation
controls under `a=a` without promising a success reply for each control. For
composition `a=c`, defined error paths return correlated failures; the reviewed
portable minimum does not require a successful `OK` reply.

Icod.Terminal therefore keeps the published continuation grammar. It does not
repeat an image id on the final chunk as a Kitty 0.49.2 workaround.

## Action and completion matrix

| Action | Published response policy | Kitty 0.49.2 observation | Successful Icod.Terminal result | Silence or failure |
| --- | --- | --- | --- | --- |
| Frame transfer or regional edit, `a=f` | Required correlated response | A final multi-chunk continuation without a repeated image id stores the frame but loses response identity and sends no acknowledgement. This is the implementation deviation corrected by `b493a63`. | `ProtocolAcknowledged` after correlated `OK` | A correlated error is definite failure. Silence after commitment is ambiguous; append also loses sequence certainty. No frame token is published. |
| Timing, selection, and playback control, `a=a` with `q=2` | No response | Successful controls are silent. | `OutputCommitted` after the complete command is written and flushed | A precommit error fails. A postcommit transport exception remains ambiguous and is not retried automatically. |
| Frame composition, `a=c` with default `q=0` | Optional success response; defined failures remain meaningful | Kitty 0.49.2 sends `OK` on success and correlated errors on documented failure paths. | Correlated `OK`: `ProtocolAcknowledged`; committed silence at the bounded response deadline: `OutputCommitted` | A correlated error is definite failure. Silence is successful output commitment without protocol-acknowledgement evidence. |

The public confirmation values describe evidence available when the call returns:

- `Unspecified` means success strength was not classified. It preserves the
  compatibility behavior of existing local/native mutations and the original
  `TerminalControlMutationResult.Success()` factory.
- `OutputCommitted` means the complete command crossed the serialized write and
  flush boundary. It does not mean the terminal parsed, applied, or rendered it.
- `ProtocolAcknowledged` means a correlated protocol success response was parsed.
  It implies output commitment, but still does not establish visible rendering.

Failures do not publish a confirmation value. A timeout or exception after bytes
may have committed is reported as ambiguous; callers must not blindly replay the
mutation. Resource cleanup remains explicit and deterministic.

## Evidence lanes

| Lane | Current 1.28 record | What it can establish |
| --- | --- | --- |
| Headless scripted transport | Covered by deterministic source tests and the sample's `--headless-transcript` mode | Exact bytes, response/no-response policy, correlation, completion strength, ambiguity, ownership, and cleanup. It never establishes rendering. |
| Kitty 0.49.2 | Source-reviewed negative multi-chunk `a=f` acknowledgement lane; live 1.28 transaction rerun `NotRun` | The exact documented deviation and, after a reviewed live run, only the scenarios actually exercised. |
| Kitty build containing `b493a63` | `NotRun` | Candidate positive lane for documented multi-chunk transfer and compose-publish. |
| Another Kitty-protocol implementation | `NotRun` | Portability check that behavior does not depend on a Kitty brand/version branch. |
| Operator-visible rendering | `NotRun` until separately consented, executed, and reviewed | Only the exact visible scenario observed by the operator. It does not strengthen unrun operations. |

`NotRun` is evidence, not a synonym for unsupported or failed. Missing,
unreviewed, or differently scoped observations remain `NotRun`; source review and
headless transport success are never promoted to a rendering result.

## Required provenance for a live report

Every reviewed live result records all of the following:

| Field | Required value |
| --- | --- |
| Scenario | Stable scenario name and revision; operation sequence and bounded command used |
| Terminal | Exact implementation name, version, build/source commit, and whether the fix commit is present |
| Platform | OS name/version/architecture and relevant distribution details |
| Host and transport | Native/PTY/WSL/container path, host application/version, and endpoint configuration |
| Icod.Terminal | Package version or exact source commit, target framework, build configuration, and runtime version |
| Time | UTC start time and bounded timeout/deadline settings |
| Transaction result | Operation, status, `Unspecified`/`OutputCommitted`/`ProtocolAcknowledged`, fallback reason, and cleanup outcome |
| Evidence | Evidence kind, source, generation/scope, raw report location, and reviewer |
| Visible result | Separately recorded `Rendered`, `Failed`, `Unavailable`, or `NotRun`, including the operator observation; never inferred from confirmation |

The runnable public-only walkthrough is
[`../samples/Icod.Terminal.RasterAnimation.Sample/README.md`](../samples/Icod.Terminal.RasterAnimation.Sample/README.md).
The approved contract and its evidence boundaries are in the
[`1.28 design`](superpowers/specs/2026-10-06-1.28.0-published-spec-kitty-graphics-transactions-design.md)
and the [`1.28 roadmap`](../Icod.Terminal-1.28.0-Development-Roadmap.md).
