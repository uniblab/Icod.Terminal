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
/// Tracks local ownership state for one persistent raster placement.
/// </summary>
internal sealed class TerminalPersistentRasterPlacementState {
	private int closed;

	internal TerminalPersistentRasterPlacementState(
		TerminalPersistentRasterResourceState resource,
		uint placementId,
		long generation
	) {
		ArgumentNullException.ThrowIfNull( resource );
		if ( 0u == placementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}
		if ( generation < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( generation ) );
		}

		this.Resource = resource;
		this.PlacementId = placementId;
		this.Generation = generation;
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

	internal bool IsClosed {
		get {
			return 0 != Volatile.Read( ref this.closed );
		}
	}

	internal void Close() {
		Interlocked.Exchange(
			ref this.closed,
			1
		);
	}
}
