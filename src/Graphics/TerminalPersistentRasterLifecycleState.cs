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
/// Stores one packed monotonic lifecycle state for a persistent raster resource or placement.
/// </summary>
internal sealed class TerminalPersistentRasterLifecycleState {
	private const int ReasonShift = 8;
	private readonly int currentValue = Pack(
		TerminalRasterOwnershipStatus.Current,
		TerminalRasterOwnershipLossReason.None
	);
	private int value;

	internal TerminalPersistentRasterLifecycleState() {
		this.value = this.currentValue;
	}

	internal TerminalRasterOwnershipState Observe() {
		return Unpack( Volatile.Read( ref this.value ) );
	}

	internal bool TryMarkStale(
		TerminalRasterOwnershipLossReason reason
	) {
		if ( reason is not TerminalRasterOwnershipLossReason.SessionStateLost
			and not TerminalRasterOwnershipLossReason.ResourceMissing
			and not TerminalRasterOwnershipLossReason.ParentPlacementLost ) {
			throw new ArgumentOutOfRangeException( nameof( reason ) );
		}

		return this.TryTransition(
			TerminalRasterOwnershipStatus.Stale,
			reason
		);
	}

	internal bool TryMarkReleased(
		TerminalRasterOwnershipLossReason reason
	) {
		if ( reason is not TerminalRasterOwnershipLossReason.AncestorReleased
			and not TerminalRasterOwnershipLossReason.ResourceReleased ) {
			throw new ArgumentOutOfRangeException( nameof( reason ) );
		}

		return this.TryTransition(
			TerminalRasterOwnershipStatus.Released,
			reason
		);
	}

	private bool TryTransition(
		TerminalRasterOwnershipStatus status,
		TerminalRasterOwnershipLossReason reason
	) {
		int desired = Pack(
			status,
			reason
		);
		return this.currentValue == Interlocked.CompareExchange(
			ref this.value,
			desired,
			this.currentValue
		);
	}

	private static int Pack(
		TerminalRasterOwnershipStatus status,
		TerminalRasterOwnershipLossReason reason
	) {
		return ( (int)status & 0xff )
			| ( ( (int)reason & 0xff ) << ReasonShift )
		;
	}

	private static TerminalRasterOwnershipState Unpack(
		int value
	) {
		return new TerminalRasterOwnershipState(
			(TerminalRasterOwnershipStatus)( value & 0xff ),
			(TerminalRasterOwnershipLossReason)( ( value >> ReasonShift ) & 0xff )
		);
	}
}
