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

	/// <summary>
	/// Assigns a positive, exact-millisecond display duration to one known frame.
	/// </summary>
	/// <param name="frame">The opaque frame token owned by this animation.</param>
	/// <param name="duration">The positive whole-millisecond display duration.</param>
	/// <param name="cancellationToken">Cancellation observed before control output commits.</param>
	/// <returns>The acknowledged semantic mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> SetFrameDurationAsync(
		TerminalRasterAnimationFrame frame,
		TimeSpan duration,
		CancellationToken cancellationToken = default
	) {
		ValidateFrame( frame );
		ValidateDuration( duration );
		cancellationToken.ThrowIfCancellationRequested();
		int gapMilliseconds = checked(
			(int)( duration.Ticks / TimeSpan.TicksPerMillisecond )
		);
		return this.resource.SetAnimationFrameDurationAsync(
			this,
			frame,
			gapMilliseconds,
			cancellationToken
		);
	}

	/// <summary>
	/// Selects one known frame as the animation's current terminal-resident frame.
	/// </summary>
	/// <param name="frame">The opaque frame token owned by this animation.</param>
	/// <param name="cancellationToken">Cancellation observed before control output commits.</param>
	/// <returns>The acknowledged semantic mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> SelectFrameAsync(
		TerminalRasterAnimationFrame frame,
		CancellationToken cancellationToken = default
	) {
		ValidateFrame( frame );
		cancellationToken.ThrowIfCancellationRequested();
		return this.resource.SelectAnimationFrameAsync(
			this,
			frame,
			cancellationToken
		);
	}

	/// <summary>
	/// Composes a bounded source-pixel region from one known frame into another known frame.
	/// </summary>
	/// <param name="source">The known source frame owned by this animation.</param>
	/// <param name="destination">The known destination frame owned by this animation.</param>
	/// <param name="sourceRectangle">A nonempty region measured in resource pixels.</param>
	/// <param name="destinationX">The zero-based destination pixel column.</param>
	/// <param name="destinationY">The zero-based destination pixel row.</param>
	/// <param name="mode">Alpha blending or replacement of destination pixels.</param>
	/// <param name="cancellationToken">Cancellation observed before output commits.</param>
	/// <returns>An acknowledged mutation result; an ambiguous committed failure must not be retried blindly.</returns>
	/// <remarks>
	/// This operation changes pixels of an existing frame, not the number or identity of frames.
	/// <see cref="State"/> reports ownership and frame-sequence certainty, not exact pixel contents.
	/// The caller remains responsible for placement, damage, and animation timing.
	/// </remarks>
	public ValueTask<TerminalControlMutationResult> ComposeFrameAsync(
		TerminalRasterAnimationFrame source,
		TerminalRasterAnimationFrame destination,
		TerminalRasterSourceRectangle sourceRectangle,
		int destinationX,
		int destinationY,
		TerminalRasterFrameCompositionMode mode = TerminalRasterFrameCompositionMode.AlphaBlend,
		CancellationToken cancellationToken = default
	) {
		this.ValidateFrame( source );
		this.ValidateFrame( destination );
		sourceRectangle.Validate();
		if ( mode is not TerminalRasterFrameCompositionMode.AlphaBlend
			and not TerminalRasterFrameCompositionMode.Replace ) {
			throw new ArgumentOutOfRangeException( nameof( mode ) );
		}

		int width = this.resource.State.SourceWidth;
		int height = this.resource.State.SourceHeight;
		if ( sourceRectangle.X > width - sourceRectangle.Width
			|| sourceRectangle.Y > height - sourceRectangle.Height ) {
			throw new ArgumentOutOfRangeException(
				nameof( sourceRectangle ),
				"The source rectangle must fit within the resource's intrinsic pixels."
			);
		}
		if ( destinationX < 0 || destinationX > width - sourceRectangle.Width ) {
			throw new ArgumentOutOfRangeException(
				nameof( destinationX ),
				"The destination rectangle must fit within the resource's intrinsic pixels."
			);
		}
		if ( destinationY < 0 || destinationY > height - sourceRectangle.Height ) {
			throw new ArgumentOutOfRangeException(
				nameof( destinationY ),
				"The destination rectangle must fit within the resource's intrinsic pixels."
			);
		}
		if ( ReferenceEquals( source, destination )
			&& sourceRectangle.X < destinationX + sourceRectangle.Width
			&& destinationX < sourceRectangle.X + sourceRectangle.Width
			&& sourceRectangle.Y < destinationY + sourceRectangle.Height
			&& destinationY < sourceRectangle.Y + sourceRectangle.Height ) {
			throw new ArgumentException(
				"Overlapping source and destination rectangles on the same frame cannot be composed.",
				nameof( destination )
			);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return this.resource.ComposeAnimationFrameAsync(
			this,
			source,
			destination,
			sourceRectangle,
			destinationX,
			destinationY,
			mode,
			cancellationToken
		);
	}

	/// <summary>
	/// Stops terminal-driven playback without releasing the owning raster resource or known frames.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before control output commits.</param>
	/// <returns>The acknowledged semantic mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> StopAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.resource.StopAnimationAsync(
			this,
			cancellationToken
		);
	}

	/// <summary>
	/// Starts loading-mode playback, which waits at the known sequence tail for later frame appends.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before control output commits.</param>
	/// <returns>The acknowledged semantic mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> RunLoadingAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.resource.RunLoadingAnimationAsync(
			this,
			cancellationToken
		);
	}

	/// <summary>
	/// Starts normal terminal-driven playback with finite or indefinite repeat policy.
	/// </summary>
	/// <param name="options">
	/// Optional semantic playback policy. A <see langword="null"/> repeat count requests indefinite looping.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before control output commits.</param>
	/// <returns>The acknowledged semantic mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> RunAsync(
		TerminalRasterAnimationPlaybackOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ValidatePlaybackOptions( options );
		cancellationToken.ThrowIfCancellationRequested();
		return this.resource.RunAnimationAsync(
			this,
			options?.RepeatCount,
			cancellationToken
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
		this.RootFrame.BindState( state.RootFrame );
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
