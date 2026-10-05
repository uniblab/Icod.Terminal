# Icod.Terminal 1.27.0 Terminal Appearance and Resize Awareness Design

## Purpose

`Icod.Terminal 1.27.0` will let applications observe two live terminal-environment facts without adding another input reader or confusing terminal reports with host state:

1. the terminal's reported dark or light appearance preference; and
2. in-band text-area resize reports containing character dimensions and optional pixel dimensions.

Both reporting facilities are explicit, independently acquired, bounded, and reversible when the terminal exposes enough state to promise restoration. Opening a session does not enable either reporting mode. The library reports observations; applications continue to own theme selection, layout, damage tracking, and repaint policy.

## Release baseline and protocol references

The stable baseline is `v1.26.0` at source commit `2c0fafaf7d7a4f6a3cbdad206960f159fb19ef95`. Its public API fingerprint is `886a617d961af7eed37feaed026d83bbf06ec508ba248a4ca492baaf7e528146`, and its production dependencies are `Icod.TermInfo 1.17.0` and `Icod.Timing 1.0.0`.

The successful stable release workflow is GitHub Actions run `37337222940`. Its `nuget-packages` artifact has artifact ZIP SHA-256 `4fe00b74d451a8c96b326f1fe4b4c46390392288d1be03ee6a6296608ab7dbc4`; the contained package hashes are:

| File | SHA-256 |
| --- | --- |
| `Icod.Terminal.1.26.0.nupkg` | `49a019d9ee8c8861ba97aeb23b6fd7f1f6b446812ad7b80cd9fd4d56cb0ace80` |
| `Icod.Terminal.1.26.0.snupkg` | `b9e17324e65efdb5dc3b684cccfdc1144154acb273589ef0df169db13042995d` |

The pre-release main workflow is run `37335193196`; all runtime, package, semantic/hardening, presentation, foundation, stable-line, and compatibility-sample jobs passed. The execution environment used to author this design does not provide `dotnet`, so runtime verification begins in CI after the contract gate.

The wire contracts are pinned as follows:

- Appearance: Contour's `docs/vt-extensions/color-palette-update-notifications.md` at repository commit `d8ce17bc34c653a3368d45f52bd7eea67452d673`, Git blob `009a502852ebed2f4b0576fe1585fc9d1dc73aaa`, reviewed 2026-10-05.
- Resize: `rockorager/e695fb2924d36b2bcf1fff4a3704bd83`, revision `a1e61ea1782326e975b6e4cfe0c538bac54c1f42`, reviewed 2026-10-05.

These documents define wire grammar, not live Icod.Terminal compatibility. Positive terminal support remains an acceptance result collected against an exact environment.

## Public API contract

All additions are in namespace `Icod.Terminal`. Existing public members and enum numeric values remain unchanged.

### Appearance value and event payload

```csharp
public enum TerminalAppearance {
	Unknown = 0,
	Dark = 1,
	Light = 2
}

public sealed class TerminalAppearanceEvent {
	public TerminalAppearance Appearance { get; }
}
```

`Unknown` is the explicit consumer/default state for “no current observation.” A valid `CSI ? 997 ; 1 n` report produces `Dark`; `CSI ? 997 ; 2 n` produces `Light`. The parser never converts an unrecognized wire value into `Unknown`: a correlated unrecognized value is malformed, and an unsolicited malformed report is discarded through bounded recovery.

`TerminalAppearanceEvent` has an internal constructor. Its `Appearance` is always `Dark` or `Light`. Repeated same-value events are preserved because an appearance report also signals a palette update and does not prove that an application theme changed.

### In-band resize payload

```csharp
public sealed class TerminalInBandResizeEvent {
	public TerminalDimensions Dimensions { get; }
	public TerminalPixelDimensions? PixelDimensions { get; }
}
```

`Dimensions` maps the report's height/width character fields to the existing `(columns, rows)` value type. `PixelDimensions` maps the height/width pixel fields to the existing `(width, height)` value type and is `null` only when both pixel fields are zero. The class has an internal constructor.

The type name deliberately preserves provenance. It is not a replacement for native `TerminalLifecycleEventKind.Resized`, `GetSize()`, or `GetDimensions()`.

### Semantic-event projection

```csharp
public enum TerminalSemanticEventKind {
	Notification = 0,
	Appearance = 1,
	InBandResize = 2
}

public sealed class TerminalSemanticEvent {
	public TerminalSemanticEventKind Kind { get; }
	public TerminalNotificationEvent? Notification { get; }
	public TerminalAppearanceEvent? Appearance { get; }
	public TerminalInBandResizeEvent? InBandResize { get; }
}
```

Exactly one payload is non-null for each semantic event. A valid unclaimed appearance or resize report is returned as one `TerminalEventKind.Semantic` event from `ReadEventAsync(...)`. Recognition is always active; acquisition controls Terminal-owned mode changes, not whether already-enabled terminal reports can be decoded.

A single in-band resize report is never republished as a lifecycle event. Native and in-band reports may describe the same size independently, and the library does not fabricate total ordering across host notifications and input bytes.

### Query and reporting acquisition

```csharp
public sealed partial class TerminalSession {
	public ValueTask<TerminalAppearance> QueryAppearanceAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlResult<TerminalAppearanceReportingLease>>
		AcquireAppearanceReportingAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken = default
		);

	public ValueTask<TerminalControlResult<TerminalInBandResizeReportingLease>>
		AcquireInBandResizeReportingAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken = default
		);
}

public sealed class TerminalAppearanceReportingLease : IAsyncDisposable {
	public ValueTask DisposeAsync();
}

public sealed class TerminalInBandResizeReportingLease : IAsyncDisposable {
	public ValueTask DisposeAsync();
}
```

All three methods require an interactive query-capable session and accept timeouts from zero through the existing one-minute `TerminalQueryTransactionManager.MaximumCallerTimeout`. They preserve the current query exception model:

- invalid timeout: `ArgumentOutOfRangeException`;
- unusable session endpoints or suspended acquisition: `InvalidOperationException`;
- caller cancellation: `OperationCanceledException`;
- no correlated reply by the deadline: `TimeoutException`;
- correlated but malformed reply: `FormatException`.

`QueryAppearanceAsync` emits only `CSI ? 996 n`; it does not enable reporting. It returns only `Dark` or `Light` after a valid correlated response. `Unknown` remains a consumer state, not a substitute for timeout, endpoint loss, or malformed data.

Reporting acquisition first queries the relevant private mode with DECRQM. A controlled `Unavailable` result means the terminal explicitly returned mode state 0 or 4. Successful output, a known terminal name, missing response, policy denial, or a malformed response cannot create `Available` or `Unavailable`. This release does not return `TerminalControlStatus.Unsupported` for a terminal's wire-level mode response because the managed implementation is present; endpoint support is an availability fact.

Leases expose no protocol-mode numbers and no mutable state. Repeated successful disposal is idempotent. A failed physical restoration retains ownership so a later disposal may retry, matching existing restoration leases.

## Wire grammar and numeric bounds

The implementation recognizes both 7-bit `ESC [` and 8-bit CSI introducers consistently with the existing framer.

| Operation | Bytes/grammar |
| --- | --- |
| Appearance query | `CSI ? 996 n` |
| Appearance response/report | `CSI ? 997 ; Ps n`, where `Ps=1` dark and `Ps=2` light |
| Appearance mode query | `CSI ? 2031 $ p` |
| Appearance enable/disable | `CSI ? 2031 h` / `CSI ? 2031 l` |
| Resize mode query | `CSI ? 2048 $ p` |
| Resize enable/disable | `CSI ? 2048 h` / `CSI ? 2048 l` |
| Mode response | `CSI ? Pm ; Ps $ y`, with the queried mode in `Pm` |
| Resize report | `CSI 48 ; rows ; columns ; pixelHeight ; pixelWidth t` |

Required primary fields may carry colon-delimited subparameters. The first value remains authoritative; unknown nonempty subparameters are ignored. Missing primary values, empty subparameters, extra semicolon fields, wrong private markers/intermediates/finals, signed values, nondecimal values, and values above `Int32.MaxValue` are invalid.

Rows and columns are each `1..Int32.MaxValue`. Pixel height and width must either both be zero or both be `1..Int32.MaxValue`; a mixed zero/nonzero pair is malformed. Pixel values are text-area pixels and never outer-window pixels. The library performs no division to invent cell dimensions.

Each response/report remains bounded by `min(4096, TerminalInputDecoderOptions.MaximumBufferedBytes)` bytes. The existing decoder buffer remains bounded to `TerminalSession.MaximumBufferedInputBytes`. No new report queue is introduced: the authoritative decoder returns one event at a time, so bursts apply backpressure to the caller and are neither coalesced nor deduplicated.

## Query ownership and correlation

The existing routing order is retained:

1. the active query transaction receives the first frame matching its response plan;
2. an unclaimed valid appearance or resize report becomes a semantic event;
3. mouse, keyboard, paste, and fallback input decoding continue.

Appearance query replies and unsolicited appearance reports have identical grammar and no request identifier. While an appearance query owns that grammar, the first matching report completes it and is not also emitted as an event. Later matching reports are semantic events. This is a protocol limitation, not proof of uniquely correlated causation. Timeout/cancellation retains the existing one-second late-response ownership window so a late response cannot become ordinary input or complete the next ambiguous query.

Mode queries are ambiguity-sensitive and run through the existing transaction manager. The existing maximum of 32 pending transactions is unchanged. No new reader, background transport loop, or independent response buffer is added.

Malformed unsolicited frames recognized by a semantic prefix are consumed only through a proven frame boundary or the existing bounded oversized-frame recovery. They cannot complete an unrelated query or consume bytes following that boundary.

## Reporting ownership and mode-state table

Appearance mode 2031 and resize mode 2048 use independent state records and owner sets in one focused `TerminalEnvironmentReportingManager`. Each facility captures one mode baseline for the current lifecycle epoch.

| DECRPM state | Meaning | Acquisition result | Appearance first-owner output | Resize first-owner output | Last-owner output |
| --- | --- | --- | --- | --- | --- |
| 0 | Not recognized | `Unavailable` | None | None | None |
| 1 | Set | `Available` | None | Re-enable to request the required initial report | None |
| 2 | Reset | `Available` | Enable | Enable | Disable |
| 3 | Permanently set | `Available` | None | Re-enable to request the required initial report | None |
| 4 | Permanently reset | `Unavailable` | None | None | None |

A malformed state is `FormatException`; silence is `TimeoutException`; cancellation remains cancellation. Unknown baseline state never produces an available lease.

Nested and concurrent acquisitions share the captured baseline and one physical enable transition. Owners have monotonically increasing session-local IDs. Out-of-order disposal removes only that owner. The final owner restores only a reset baseline that Icod.Terminal changed to set; an externally set or permanently set baseline remains set.

The enable or resize re-enable write is serialized through the session's control-output gate. Cancellation is honored until the write commits. After commitment, ambiguous failure follows existing output-certainty rules: no blind retry and no claim that remote state is known. Acquisition failure after an attempted enable performs one best-effort cleanup only when the captured baseline proves that disabling is correct; a cleanup failure is aggregated with the acquisition failure.

Mode 2048 requires an immediate report when enabled or re-enabled. Acquisition does not consume or fabricate that report. The bytes remain with the authoritative input path and become an `InBandResize` semantic event when the caller reads events.

## Lifecycle, invalidation, and disposal

The reporting manager is a core `ITerminalObservedLifecycleParticipant`.

Suspend ordering is:

1. public query transactions are suspended;
2. reporting state owned from a captured reset baseline is disabled;
3. existing input/presentation/session state leaves through current ordering.

Resume ordering remains the repository's established sequence:

1. host input/output state, presentation state, and input protocols re-enter;
2. the internal lifecycle-observation query window opens while public queries remain suspended;
3. the reporting manager re-queries only facilities with active owners;
4. reset states are re-enabled during participant resume; set/permanently-set states require no write;
5. public query transactions resume.

An unsupported/permanently-reset/malformed/silent refresh with active owners fails lifecycle re-entry rather than claiming reporting resumed. All queries occur outside the output gate and no reply is awaited while holding a gate required by the decoder or query writer.

`InvalidateState()` also invalidates reporting baselines and retained freshness. Active leases remain logical owners, but no retained appearance or resize observation is exposed as “current.” An explicit invalidation cannot preserve the prior epoch's exact restoration promise. The next acquisition or lifecycle refresh establishes a new observed baseline; until then, final release performs no blind mode write. This conservative behavior may leave an externally changed reporting mode enabled, but never disables a mode whose current ownership is unknown.

Session disposal keeps the existing outer order: stop accepting ordinary session output, close query transactions, stop lifecycle input, then close environment reporting before input-protocol and presentation state and before restoring the host mode. Reporting close performs no query. With a still-valid reset baseline, it emits the one owned disable and aggregates cleanup failure into the session disposal error. With invalidated ownership it emits no speculative toggle. All leases are marked released when manager closure completes. A successful write means only local emission completed, not that the terminal applied the restoration.

The concurrency order is fixed as state-composition gate, reporting-manager gate, query ambiguity ownership, and then the control-output gate for request emission only. The control-output gate is released before waiting for a reply. The decoder acquires neither the state-composition nor reporting-manager gate. No path acquires the reporting-manager gate while already holding control output, and no manager path calls `ReadEventAsync(...)`.

## Source boundaries

The implementation adds focused files rather than expanding unrelated protocol classes:

| File | Responsibility |
| --- | --- |
| `src/Environment/TerminalAppearance.cs` | Appearance enum and payload. |
| `src/Environment/TerminalInBandResizeEvent.cs` | Typed resize payload and provenance. |
| `src/Environment/TerminalEnvironmentProtocol.cs` | Requests, matchers, strict parsers, and mode writes for 996/997/2031/2048/48. |
| `src/Environment/TerminalPrivateModeState.cs` | Internal DECRPM state model. |
| `src/Environment/TerminalEnvironmentReportingManager.cs` | Independent mode ownership, lifecycle refresh, invalidation, and close. |
| `src/Environment/TerminalAppearanceReportingLease.cs` | Appearance owner token. |
| `src/Environment/TerminalInBandResizeReportingLease.cs` | Resize owner token. |
| `src/Session/TerminalSession.Environment.cs` | Public query/acquisition surface and session integration. |
| `src/Input/TerminalSemanticEvent.cs` | Additive semantic kinds and payload properties. |
| `src/Input/TerminalInputDecoder.SemanticEvents.cs` | General semantic CSI/OSC report recognition after query ownership. |
| `src/Session/TerminalSession.cs` | Manager construction, invalidation, and disposal integration only. |

No Icod.TermInfo capability, parser, database, or package-version change is required. The protocols are runtime-negotiated and cannot be truthfully inferred from terminfo.

## Test contract

Tests use deterministic recording transports and clocks; no timing-dependent sleeps are accepted.

- Protocol fixtures cover every byte split, 7-bit/8-bit CSI, concatenated frames, interleaved ordinary input, valid subparameters, wrong markers/finals, missing/extra fields, signed/nondecimal data, integer overflow, mixed-zero pixels, oversized frames, and recovery to following input.
- Query fixtures cover dark/light, malformed/unknown values, timeout, cancellation, endpoint loss, racing reports, late ownership, and no implicit mode enable.
- Ownership fixtures cover all five DECRPM states, nested/concurrent acquisition, out-of-order disposal, already-enabled state, enable failure, restoration failure/retry, invalidation, and independent appearance/resize owners.
- Event fixtures prove repeated appearance delivery, pixel-only resize delivery, unknown pixels, no resize deduplication, no lifecycle duplication, operation without a native lifecycle source, and unchanged synchronous geometry methods.
- Lifecycle fixtures cover suspend/resume/invalidation/disposal with deterministic gate interleavings, query-window order, both modes active, input EOF, endpoint failure, and aggregated cleanup errors.
- Compatibility acceptance adds separately revisioned public-only scenarios for appearance query, appearance reporting, and in-band resize reporting. Reporting scenarios require operator consent before mode acquisition.

## Documentation and compatibility evidence

README, query/input/lifecycle guidance, security notes, sample documentation, changelog, and curated `docs/releases/1.27.0.md` must describe only delivered behavior. The compatibility sample records terminal/OS/transport identity, scenario revision, bounded outcomes, and observations without raw input, arbitrary replies, environment dumps, or host identity.

Stable release requires:

- one positive bounded appearance query and one operator-induced appearance report on an exact supporting environment;
- an initial and changed in-band resize report on an exact supporting environment;
- an unsupported or missing-reporting lane that preserves native resize and ordinary input behavior;
- successful source, runtime, package, API, XML, dependency, license, documentation, sample, matrix, and artifact verification at one exact head.

If either selected feature lacks a positive live witness, the release does not silently waive the gap or infer support from CI.

## Non-goals

Version 1.27 does not add host-theme detection, color-luminance inference, automatic application theme changes, layout or repaint policy, a cached replacement for `GetSize()`/`GetDimensions()`, a generic raw private-mode API, OSC 21, OSC 5522, ReportCellSize, transport forwarding, Icod.TermInfo changes, PTY hosting, graphics work, or Kitty-specific workarounds.
