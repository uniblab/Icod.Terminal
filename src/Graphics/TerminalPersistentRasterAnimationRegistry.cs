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
/// Owns bounded session-scoped bookkeeping for persistent raster animation frame identities.
/// </summary>
internal sealed class TerminalPersistentRasterAnimationRegistry {
	internal const int MaximumKnownFrames = 4096;

	private readonly object synchronization = new();
	private readonly Dictionary<
		TerminalPersistentRasterResourceState,
		TerminalPersistentRasterAnimationState
	> animations = [];
	private readonly Dictionary<
		TerminalPersistentRasterAnimationState,
		HashSet<TerminalPersistentRasterAnimationFrameState>
	> frames = [];
	private readonly Dictionary<
		TerminalPersistentRasterAnimationState,
		AppendReservation
	> pendingAppends = [];
	private int knownFrameCount;

	internal int KnownFrameCount {
		get {
			lock ( this.synchronization ) {
				return this.knownFrameCount;
			}
		}
	}

	internal bool TryGetOrCreate(
		TerminalPersistentRasterResourceState resource,
		out TerminalPersistentRasterAnimationState? animation
	) {
		ArgumentNullException.ThrowIfNull( resource );

		lock ( this.synchronization ) {
			if ( this.animations.TryGetValue(
				resource,
				out TerminalPersistentRasterAnimationState? existing
			) ) {
				animation = this.IsResourceCurrent( resource )
					? existing
					: null
				;
				return animation is not null;
			}

			if ( !this.IsResourceCurrent( resource )
				|| MaximumKnownFrames <= this.GetAllocatedFrameCountUnsafe() ) {
				animation = null;
				return false;
			}

			animation = new TerminalPersistentRasterAnimationState( resource );
			resource.BindAnimationState( animation );
			this.animations.Add(
				resource,
				animation
			);
			this.frames.Add(
				animation,
				[ animation.RootFrame ]
			);
			++this.knownFrameCount;
			return true;
		}
	}

	internal bool TryReserveAppend(
		TerminalPersistentRasterAnimationState animation,
		out AppendReservation? reservation
	) {
		ArgumentNullException.ThrowIfNull( animation );

		lock ( this.synchronization ) {
			if ( !this.IsRegisteredAnimationUnsafe( animation )
				|| TerminalRasterAnimationStatus.Current != animation.ObserveState().Status
				|| this.pendingAppends.ContainsKey( animation )
				|| MaximumKnownFrames <= this.GetAllocatedFrameCountUnsafe() ) {
				reservation = null;
				return false;
			}

			uint frameNumber = checked( (uint)animation.KnownFrameCount + 1u );
			reservation = new AppendReservation(
				this,
				animation,
				frameNumber
			);
			this.pendingAppends.Add(
				animation,
				reservation
			);
			return true;
		}
	}

	internal bool TryRollbackAppend(
		AppendReservation reservation
	) {
		ArgumentNullException.ThrowIfNull( reservation );

		lock ( this.synchronization ) {
			if ( !ReferenceEquals(
				this,
				reservation.Owner
			) || !this.pendingAppends.TryGetValue(
				reservation.Animation,
				out AppendReservation? pending
			) || !ReferenceEquals(
				pending,
				reservation
			) ) {
				return false;
			}

			return this.pendingAppends.Remove( reservation.Animation );
		}
	}

	internal bool TryPublishAppend(
		AppendReservation reservation,
		out TerminalPersistentRasterAnimationFrameState? frame
	) {
		ArgumentNullException.ThrowIfNull( reservation );

		lock ( this.synchronization ) {
			if ( !ReferenceEquals(
				this,
				reservation.Owner
			) || !this.pendingAppends.TryGetValue(
				reservation.Animation,
				out AppendReservation? pending
			) || !ReferenceEquals(
				pending,
				reservation
			) || !this.IsRegisteredAnimationUnsafe( reservation.Animation )
				|| TerminalRasterAnimationStatus.Current
					!= reservation.Animation.ObserveState().Status ) {
				frame = null;
				return false;
			}

			uint expected = checked( (uint)reservation.Animation.KnownFrameCount + 1u );
			if ( expected != reservation.FrameNumber ) {
				throw new InvalidOperationException(
					"The animation append reservation no longer matches the known frame sequence."
				);
			}

			frame = new TerminalPersistentRasterAnimationFrameState(
				reservation.Animation,
				reservation.FrameNumber
			);
			this.frames[ reservation.Animation ].Add( frame );
			_ = this.pendingAppends.Remove( reservation.Animation );
			reservation.Animation.PublishFrame();
			++this.knownFrameCount;
			return true;
		}
	}

	internal bool OwnsFrame(
		TerminalPersistentRasterAnimationState animation,
		TerminalPersistentRasterAnimationFrameState frame
	) {
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( frame );

		lock ( this.synchronization ) {
			return this.IsRegisteredAnimationUnsafe( animation )
				&& ReferenceEquals(
					animation,
					frame.Animation
				) && this.frames.TryGetValue(
					animation,
					out HashSet<TerminalPersistentRasterAnimationFrameState>? knownFrames
				) && knownFrames.Contains( frame );
		}
	}

	internal void Invalidate() {
		lock ( this.synchronization ) {
			foreach ( TerminalPersistentRasterAnimationState animation in this.animations.Values ) {
				_ = animation.TryMarkStale(
					TerminalRasterAnimationLossReason.SessionStateLost
				);
			}
			this.ClearUnsafe();
		}
	}

	internal bool InvalidateResource(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );
		return this.RemoveResource(
			resource,
			static animation => animation.TryMarkStale(
				TerminalRasterAnimationLossReason.ResourceMissing
			)
		);
	}

	internal bool ReleaseResource(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );
		return this.RemoveResource(
			resource,
			static animation => animation.TryMarkReleased()
		);
	}

	internal bool DisposeResourceOwner(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );
		return this.RemoveResource(
			resource,
			static animation => animation.TryMarkOwnerDisposed()
		);
	}

	private bool RemoveResource(
		TerminalPersistentRasterResourceState resource,
		Func<TerminalPersistentRasterAnimationState, bool> transition
	) {
		ArgumentNullException.ThrowIfNull( transition );

		lock ( this.synchronization ) {
			if ( !this.animations.Remove(
				resource,
				out TerminalPersistentRasterAnimationState? animation
			) ) {
				return false;
			}

			_ = transition( animation );
			_ = this.pendingAppends.Remove( animation );
			if ( this.frames.Remove(
				animation,
				out HashSet<TerminalPersistentRasterAnimationFrameState>? knownFrames
			) ) {
				this.knownFrameCount = checked(
					this.knownFrameCount - knownFrames.Count
				);
			}
			return true;
		}
	}

	private bool IsRegisteredAnimationUnsafe(
		TerminalPersistentRasterAnimationState animation
	) {
		return this.animations.TryGetValue(
			animation.Resource,
			out TerminalPersistentRasterAnimationState? registered
		) && ReferenceEquals(
			animation,
			registered
		);
	}

	private int GetAllocatedFrameCountUnsafe() {
		return checked(
			this.knownFrameCount + this.pendingAppends.Count
		);
	}

	private bool IsResourceCurrent(
		TerminalPersistentRasterResourceState resource
	) {
		return !resource.IsClosed
			&& TerminalRasterOwnershipStatus.Current == resource.ObserveOwnershipState().Status;
	}

	private void ClearUnsafe() {
		this.animations.Clear();
		this.frames.Clear();
		this.pendingAppends.Clear();
		this.knownFrameCount = 0;
	}

	internal sealed class AppendReservation {
		internal AppendReservation(
			TerminalPersistentRasterAnimationRegistry owner,
			TerminalPersistentRasterAnimationState animation,
			uint frameNumber
		) {
			ArgumentNullException.ThrowIfNull( owner );
			ArgumentNullException.ThrowIfNull( animation );
			if ( frameNumber < 2u ) {
				throw new ArgumentOutOfRangeException( nameof( frameNumber ) );
			}

			this.Owner = owner;
			this.Animation = animation;
			this.FrameNumber = frameNumber;
		}

		internal TerminalPersistentRasterAnimationRegistry Owner {
			get;
		}

		internal TerminalPersistentRasterAnimationState Animation {
			get;
		}

		internal uint FrameNumber {
			get;
		}
	}
}
