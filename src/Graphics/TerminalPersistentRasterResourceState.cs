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
/// Tracks local ownership state for one persistent raster resource.
/// </summary>
internal sealed class TerminalPersistentRasterResourceState {
	private int imageIdBits;
	private int closed;

	internal TerminalPersistentRasterResourceState(
		uint imageNumber,
		long generation
	) {
		if ( 0u == imageNumber ) {
			throw new ArgumentOutOfRangeException( nameof( imageNumber ) );
		}
		if ( generation < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( generation ) );
		}

		this.ImageNumber = imageNumber;
		this.Generation = generation;
	}

	internal uint ImageNumber {
		get;
	}

	internal uint ImageId {
		get {
			return unchecked( (uint)Volatile.Read( ref this.imageIdBits ) );
		}
	}

	internal long Generation {
		get;
	}

	internal bool IsClosed {
		get {
			return 0 != Volatile.Read( ref this.closed );
		}
	}

	internal void BindImageId(
		uint imageId
	) {
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}

		int bits = unchecked( (int)imageId );
		int prior = Interlocked.CompareExchange(
			ref this.imageIdBits,
			bits,
			0
		);
		if ( 0 != prior && imageId != unchecked( (uint)prior ) ) {
			throw new InvalidOperationException(
				"The persistent raster resource already has a different terminal-assigned image id."
			);
		}
	}

	internal void Close() {
		Interlocked.Exchange(
			ref this.closed,
			1
		);
	}
}
