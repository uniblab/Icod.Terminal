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
/// Tracks local ownership state for one persistent virtual raster placeholder placement.
/// </summary>
internal sealed class TerminalPersistentRasterPlaceholderState {
	private readonly TerminalPersistentRasterLifecycleState lifecycle = new();
	private int closed;

	internal TerminalPersistentRasterPlaceholderState(
		TerminalPersistentRasterResourceState resource,
		uint placementId,
		long generation,
		int columns,
		int rows
	) {
		ArgumentNullException.ThrowIfNull( resource );
		if ( placementId is 0u or > TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}
		if ( generation < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( generation ) );
		}
		if ( columns is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException( nameof( columns ) );
		}
		if ( rows is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException( nameof( rows ) );
		}

		this.Resource = resource;
		this.PlacementId = placementId;
		this.Generation = generation;
		this.Columns = columns;
		this.Rows = rows;
	}

	internal TerminalPersistentRasterResourceState Resource {
		get;
	}

	internal uint PlacementId {
		get;
	}

	internal long Generation {
		get;
	}

	internal int Columns {
		get;
	}

	internal int Rows {
		get;
	}

	internal bool IsClosed {
		get {
			return 0 != Volatile.Read( ref this.closed );
		}
	}

	internal TerminalRasterOwnershipState ObserveOwnershipState() {
		return this.lifecycle.Observe();
	}

	internal bool TryMarkStale(
		TerminalRasterOwnershipLossReason reason
	) {
		return this.lifecycle.TryMarkStale( reason );
	}

	internal bool TryMarkReleased(
		TerminalRasterOwnershipLossReason reason
	) {
		return this.lifecycle.TryMarkReleased( reason );
	}

	internal void Close() {
		Interlocked.Exchange(
			ref this.closed,
			1
		);
	}
}
