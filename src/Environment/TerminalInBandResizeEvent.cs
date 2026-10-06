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
/// Represents one terminal-reported in-band text-area resize observation.
/// </summary>
/// <remarks>
/// This observation does not replace native lifecycle resize events or synchronous
/// terminal-dimension APIs. Its dimensions retain their in-band provenance.
/// </remarks>
public sealed class TerminalInBandResizeEvent {
	internal TerminalInBandResizeEvent(
		TerminalDimensions dimensions,
		TerminalPixelDimensions? pixelDimensions
	) {
		this.Dimensions = dimensions;
		this.PixelDimensions = pixelDimensions;
	}

	/// <summary>Gets the terminal-reported character-cell dimensions.</summary>
	public TerminalDimensions Dimensions {
		get;
	}

	/// <summary>
	/// Gets the terminal-reported text-area pixel dimensions, or <see langword="null"/>
	/// when the report says pixel dimensions are unavailable.
	/// </summary>
	public TerminalPixelDimensions? PixelDimensions {
		get;
	}
}
