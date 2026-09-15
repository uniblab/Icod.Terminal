/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>
/// Provides resource-owned semantic control of one persistent-raster animation sequence.
/// </summary>
/// <remarks>
/// The controller owns no independent terminal resource and is not independently disposable.
/// The owning <see cref="TerminalRasterResource"/> remains final cleanup authority.
/// </remarks>
public sealed class TerminalRasterAnimation {
	private const string NotImplementedMessage =
		"Persistent-raster animation control is not implemented by this development checkpoint.";

	private readonly TerminalRasterResource resource;
	private TerminalPersistentRasterAnimationState? animationState;

	internal TerminalRasterAnimation(
		TerminalRasterResource resource
	) {
		ArgumentNullException.ThrowIfNull( resource );

		this.resource = resource;
		this.RootFrame = new TerminalRasterAnimationFrame( this, 1 );
	}

	/// <summary>
	/// Gets the opaque root frame represented by the owning resource's original raster content.
	/// </summary>
	public TerminalRasterAnimationFrame RootFrame {
		get;
	}

	/// <summary>
	/// Gets one side-effect-free snapshot of Icod.Terminal's current local animation certainty.
	/// </summary>
	public TerminalRasterAnimationState State {
		get {
			TerminalRasterOwnershipState ownership = this.resource.OwnershipState;
			if ( TerminalRasterOwnershipStatus.Current == ownership.Status ) {
				TerminalPersistentRasterAnimationState? state = Volatile.Read(
					ref this.animationState
				);
				return state is null
					? new TerminalRasterAnimationState(
						TerminalRasterAnimationStatus.Current,
						TerminalRasterAnimationLossReason.None
					)
					: state.ObserveState()
				;
			}

			return ownership.Status switch {
				TerminalRasterOwnershipStatus.Stale =>
					new TerminalRasterAnimationState(
						TerminalRasterAnimationStatus.Stale,
						ConvertLossReason( ownership.LossReason )
					),
				TerminalRasterOwnershipStatus.Released =>
					new TerminalRasterAnimationState(
						TerminalRasterAnimationStatus.Released,
						ConvertLossReason( ownership.LossReason )
					),
				TerminalRasterOwnershipStatus.Disposed =>
					new TerminalRasterAnimationState(
						TerminalRasterAnimationStatus.OwnerDisposed,
						TerminalRasterAnimationLossReason.ExplicitResourceDisposal
					),
				_ => throw new InvalidOperationException(
					"The raster resource contains an unknown ownership status."
				)
			};
		}
	}

	/// <summary>
	/// Appends one full-size frame to this resource's terminal-resident animation sequence.
	/// </summary>
	public ValueTask<TerminalControlResult<TerminalRasterAnimationFrame>> AddFrameAsync(
		TerminalRasterImage image,
		TimeSpan duration,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( image );
		ValidateDuration( duration );
		cancellationToken.ThrowIfCancellationRequested();
		if ( image.Width != this.resource.State.SourceWidth
			|| image.Height != this.resource.State.SourceHeight ) {
			throw new ArgumentException(
				"An animation frame must match the owning raster resource's intrinsic dimensions.",
				nameof( image )
			);
		}

		int gapMilliseconds = checked(
			(int)( duration.Ticks / TimeSpan.TicksPerMillisecond )
		);
		return this.resource.AddAnimationFrameAsync(
			this,
			image,
			gapMilliseconds,
			cancellationToken
		);
	}

	public ValueTask<TerminalControlMutationResult> SetFrameDurationAsync(
		TerminalRasterAnimationFrame frame,
		TimeSpan duration,
		CancellationToken cancellationToken = default
	) {
		ValidateFrame( frame );
		ValidateDuration( duration );
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			TerminalControlMutationResult.Unsupported( NotImplementedMessage )
		);
	}

	public ValueTask<TerminalControlMutationResult> SelectFrameAsync(
		TerminalRasterAnimationFrame frame,
		CancellationToken cancellationToken = default
	) {
		ValidateFrame( frame );
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			TerminalControlMutationResult.Unsupported( NotImplementedMessage )
		);
	}

	public ValueTask<TerminalControlMutationResult> StopAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			TerminalControlMutationResult.Unsupported( NotImplementedMessage )
		);
	}

	public ValueTask<TerminalControlMutationResult> RunLoadingAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			TerminalControlMutationResult.Unsupported( NotImplementedMessage )
		);
	}

	public ValueTask<TerminalControlMutationResult> RunAsync(
		TerminalRasterAnimationPlaybackOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ValidatePlaybackOptions( options );
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			TerminalControlMutationResult.Unsupported( NotImplementedMessage )
		);
	}

	internal void BindState(
		TerminalPersistentRasterAnimationState state
	) {
		ArgumentNullException.ThrowIfNull( state );
		if ( !ReferenceEquals( this.resource.State, state.Resource ) ) {
			throw new ArgumentException(
				"The animation state must belong to the controller's persistent raster resource.",
				nameof( state )
			);
		}

		TerminalPersistentRasterAnimationState? prior = Interlocked.CompareExchange(
			ref this.animationState,
			state,
			null
		);
		if ( prior is not null && !ReferenceEquals( prior, state ) ) {
			throw new InvalidOperationException(
				"The persistent-raster animation controller is already bound to different internal state."
			);
		}
	}

	private void ValidateFrame(
		TerminalRasterAnimationFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( !ReferenceEquals( this, frame.Owner ) ) {
			throw new ArgumentException(
				"The animation frame must belong to this persistent-raster animation.",
				nameof( frame )
			);
		}
	}

	private static void ValidateDuration(
		TimeSpan duration
	) {
		long ticks = duration.Ticks;
		if ( ticks < TimeSpan.TicksPerMillisecond
			|| 0 != ticks % TimeSpan.TicksPerMillisecond
			|| (long)int.MaxValue * TimeSpan.TicksPerMillisecond < ticks ) {
			throw new ArgumentOutOfRangeException(
				nameof( duration ),
				duration,
				$"An animation-frame duration must be an exact whole number of milliseconds between 1 and {int.MaxValue}."
			);
		}
	}

	private static void ValidatePlaybackOptions(
		TerminalRasterAnimationPlaybackOptions? options
	) {
		if ( options?.RepeatCount is not int repeatCount ) {
			return;
		}
		if ( repeatCount < 1 || int.MaxValue == repeatCount ) {
			throw new ArgumentOutOfRangeException(
				nameof( options ),
				repeatCount,
				$"A finite animation repeat count must be between 1 and {int.MaxValue - 1}."
			);
		}
	}

	private static TerminalRasterAnimationLossReason ConvertLossReason(
		TerminalRasterOwnershipLossReason reason
	) {
		return reason switch {
			TerminalRasterOwnershipLossReason.None => TerminalRasterAnimationLossReason.None,
			TerminalRasterOwnershipLossReason.SessionStateLost => TerminalRasterAnimationLossReason.SessionStateLost,
			TerminalRasterOwnershipLossReason.ResourceMissing => TerminalRasterAnimationLossReason.ResourceMissing,
			TerminalRasterOwnershipLossReason.ResourceReleased => TerminalRasterAnimationLossReason.ResourceReleased,
			TerminalRasterOwnershipLossReason.ExplicitDisposal => TerminalRasterAnimationLossReason.ExplicitResourceDisposal,
			_ => throw new InvalidOperationException(
				"A raster resource reported a placement-only ownership loss reason."
			)
		};
	}
}
