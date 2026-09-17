# Icod.Terminal 1.16 Public API Baseline

This document records the additive public API frozen for `Icod.Terminal 1.16.0` Persistent Raster Animation and Frame Lifecycle.

The stable compatibility floor remains `1.0.0`. The complete 1.15 public surface remains available and unchanged. The predecessor 1.15 public API fingerprint is:

```text
eb361cef615fda97ac2c0ef9da8ea3d63fdc1f537ec438164bcb93694eecd13d
```

The final generated public API snapshots are identical on `net8.0`, `net9.0`, and `net10.0`. The stable 1.16 fingerprint is:

```text
d2acfa85aad87c739b3f682096d4d7139627f12bc9d8981b65529eeb79a2da8d
```

The machine-readable fingerprint is stored separately in `docs/Public-API-Baseline-1.16.sha256` and is enforced by the package/public-API gate for the stable 1.16 release line.

## Public additions over 1.15

### Semantic capability

`TerminalCapability` adds exactly one value while preserving every previously released numeric value:

```csharp
TerminalCapability.PersistentRasterAnimation = 11
```

Animation is a distinct semantic capability. Ordinary raster display, persistent raster ownership, Unicode raster placeholders, and persistent raster animation remain independently represented.

### Animation certainty

```csharp
public enum TerminalRasterAnimationStatus {
	Current = 0,
	SequenceUncertain = 1,
	Stale = 2,
	Released = 3,
	OwnerDisposed = 4
}

public enum TerminalRasterAnimationLossReason {
	None = 0,
	FrameSequenceAmbiguous = 1,
	SessionStateLost = 2,
	ResourceMissing = 3,
	ResourceReleased = 4,
	ExplicitResourceDisposal = 5
}

public readonly record struct TerminalRasterAnimationState(
	TerminalRasterAnimationStatus Status,
	TerminalRasterAnimationLossReason LossReason
);
```

Animation certainty is intentionally separate from `TerminalRasterOwnershipState`. A committed append may leave the exact terminal-side frame tail uncertain without falsely invalidating the owning raster resource.

### Playback options

```csharp
public sealed class TerminalRasterAnimationPlaybackOptions {
	public int? RepeatCount { get; init; }
}
```

`RepeatCount` is semantic: `null` requests indefinite terminal-driven looping; a finite value represents additional traversals after the first traversal and must be positive. The public API does not expose Kitty's raw loop-count encoding.

### Opaque frame token

```csharp
public sealed class TerminalRasterAnimationFrame {
}
```

The frame type has no public constructor and exposes no public numeric frame identity. The resource's original raster content is represented by the opaque `RootFrame` token.

### Resource-owned animation controller

```csharp
public sealed class TerminalRasterAnimation {
	public TerminalRasterAnimationFrame RootFrame { get; }
	public TerminalRasterAnimationState State { get; }

	public ValueTask<TerminalControlResult<TerminalRasterAnimationFrame>> AddFrameAsync(
		TerminalRasterImage image,
		TimeSpan duration,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlMutationResult> SetFrameDurationAsync(
		TerminalRasterAnimationFrame frame,
		TimeSpan duration,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlMutationResult> SelectFrameAsync(
		TerminalRasterAnimationFrame frame,
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlMutationResult> StopAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlMutationResult> RunLoadingAsync(
		CancellationToken cancellationToken = default
	);

	public ValueTask<TerminalControlMutationResult> RunAsync(
		TerminalRasterAnimationPlaybackOptions? options = null,
		CancellationToken cancellationToken = default
	);
}
```

`TerminalRasterAnimation` has no public constructor and does not implement `IDisposable` or `IAsyncDisposable`. It owns no second terminal resource. The owning `TerminalRasterResource` remains final lifecycle and cleanup authority.

`TerminalRasterResource` adds:

```csharp
public TerminalRasterAnimation Animation { get; }
```

The property is side-effect free and performs no terminal I/O.

## Local validation frozen in T160

The semantic API freezes these local bounds before animation transport is implemented:

- appended images must match the owning resource's intrinsic pixel dimensions;
- visible frame durations must be exact whole milliseconds in `1..Int32.MaxValue`;
- finite `RepeatCount` values must be in `1..Int32.MaxValue-1` so private conversion to the protocol loop count is checked and unambiguous;
- frame arguments must belong to the target animation controller;
- caller cancellation observed before commitment is propagated locally.

The completed 1.16 implementation applies these validations before protocol output and routes accepted operations through the acknowledged animation transaction path.

## Identity and protocol boundary

The 1.16 public additions expose no:

- Kitty image id or image number;
- Kitty frame number;
- session generation number;
- raw `a=f`, `a=a`, `s`, `v`, `c`, `r`, or `z` protocol fields;
- APC construction helper;
- public animation backend selector;
- frame-composition command or delta-edit API.

The public vocabulary remains semantic: resource-owned animation, opaque frame token, local certainty, duration, frame selection, stop/loading/run, and repeat policy.

## Compatibility and non-goals

Version 1.16 remains additive over the stable `1.0.0` compatibility floor and complete 1.15 surface.

Existing persistent resources, physical/relative placements, Unicode placeholders, lifecycle observation, generation invalidation, capacity ceilings, cleanup ordering, no-replay semantics, and caller-owned layout behavior remain unchanged when animation APIs are unused.

Version 1.16 does not add frame-to-frame composition, partial/delta frame edits, gapless composition frames, absolute screen-coordinate placement, pixel-within-cell positioning, GIF/APNG/image decoding, retained source-frame replay caches, audio/timeline synchronization, Terminal-owned scene/window/cell/damage/layout policy, public Kitty identities, or PTY/ConPTY hosting.
