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
/// Tracks local sequence certainty for one resource-owned persistent raster animation.
/// </summary>
internal sealed class TerminalPersistentRasterAnimationState {
	private const int ReasonShift = 8;
	private int stateValue = Pack(
		TerminalRasterAnimationStatus.Current,
		TerminalRasterAnimationLossReason.None
	);
	private int knownFrameCount = 1;

	internal TerminalPersistentRasterAnimationState(
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( resource );

		this.Resource = resource;
		this.RootFrame = new TerminalPersistentRasterAnimationFrameState(
			this,
			frameNumber: 1
		);
	}

	internal TerminalPersistentRasterResourceState Resource {
		get;
	}

	internal TerminalPersistentRasterAnimationFrameState RootFrame {
		get;
	}

	internal int KnownFrameCount {
		get {
			return Volatile.Read( ref this.knownFrameCount );
		}
	}

	internal TerminalRasterAnimationState ObserveState() {
		return Unpack( Volatile.Read( ref this.stateValue ) );
	}

	internal bool TryMarkSequenceUncertain() {
		return this.TryTransition(
			TerminalRasterAnimationStatus.SequenceUncertain,
			TerminalRasterAnimationLossReason.FrameSequenceAmbiguous
		);
	}

	internal bool TryMarkStale(
		TerminalRasterAnimationLossReason reason
	) {
		if ( reason is not TerminalRasterAnimationLossReason.SessionStateLost
			and not TerminalRasterAnimationLossReason.ResourceMissing ) {
			throw new ArgumentOutOfRangeException( nameof( reason ) );
		}

		return this.TryTransition(
			TerminalRasterAnimationStatus.Stale,
			reason
		);
	}

	internal bool TryMarkReleased() {
		return this.TryTransition(
			TerminalRasterAnimationStatus.Released,
			TerminalRasterAnimationLossReason.ResourceReleased
		);
	}

	internal bool TryMarkOwnerDisposed() {
		return this.TryTransition(
			TerminalRasterAnimationStatus.OwnerDisposed,
			TerminalRasterAnimationLossReason.ExplicitResourceDisposal
		);
	}

	internal void PublishFrame() {
		int count = Interlocked.Increment( ref this.knownFrameCount );
		if ( count > TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames ) {
			throw new InvalidOperationException(
				"The animation frame count exceeded the bounded registry ceiling."
			);
		}
	}

	private bool TryTransition(
		TerminalRasterAnimationStatus status,
		TerminalRasterAnimationLossReason reason
	) {
		int desired = Pack(
			status,
			reason
		);
		while ( true ) {
			int prior = Volatile.Read( ref this.stateValue );
			TerminalRasterAnimationStatus priorStatus = UnpackStatus( prior );
			if ( (int)status <= (int)priorStatus ) {
				return false;
			}
			if ( prior == Interlocked.CompareExchange(
				ref this.stateValue,
				desired,
				prior
			) ) {
				return true;
			}
		}
	}

	private static int Pack(
		TerminalRasterAnimationStatus status,
		TerminalRasterAnimationLossReason reason
	) {
		return ( (int)status & 0xff )
			| ( ( (int)reason & 0xff ) << ReasonShift )
		;
	}

	private static TerminalRasterAnimationState Unpack(
		int value
	) {
		return new TerminalRasterAnimationState(
			UnpackStatus( value ),
			(TerminalRasterAnimationLossReason)( ( value >> ReasonShift ) & 0xff )
		);
	}

	private static TerminalRasterAnimationStatus UnpackStatus(
		int value
	) {
		return (TerminalRasterAnimationStatus)( value & 0xff );
	}
}
