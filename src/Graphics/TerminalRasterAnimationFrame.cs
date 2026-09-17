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
/// Represents one opaque, locally known frame in a persistent-raster animation sequence.
/// </summary>
public sealed class TerminalRasterAnimationFrame {
	private TerminalPersistentRasterAnimationFrameState? state;

	internal TerminalRasterAnimationFrame(
		TerminalRasterAnimation owner,
		int sequenceNumber
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( sequenceNumber < 1 ) {
			throw new ArgumentOutOfRangeException(
				nameof( sequenceNumber ),
				sequenceNumber,
				"An animation frame sequence number must be positive."
			);
		}

		this.Owner = owner;
		this.SequenceNumber = sequenceNumber;
	}

	internal TerminalRasterAnimation Owner {
		get;
	}

	internal int SequenceNumber {
		get;
	}

	internal TerminalPersistentRasterAnimationFrameState? State {
		get {
			return Volatile.Read( ref this.state );
		}
	}

	internal void BindState(
		TerminalPersistentRasterAnimationFrameState value
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( checked( (int)value.FrameNumber ) != this.SequenceNumber ) {
			throw new ArgumentException(
				"The private animation frame state does not match the opaque frame token sequence.",
				nameof( value )
			);
		}

		TerminalPersistentRasterAnimationFrameState? prior = Interlocked.CompareExchange(
			ref this.state,
			value,
			null
		);
		if ( prior is not null && !ReferenceEquals( prior, value ) ) {
			throw new InvalidOperationException(
				"The opaque animation frame token is already bound to different private state."
			);
		}
	}
}
