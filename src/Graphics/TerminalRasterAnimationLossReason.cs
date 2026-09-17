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
/// Describes why one persistent-raster animation is no longer fully current.
/// </summary>
public enum TerminalRasterAnimationLossReason {
	/// <summary>No animation certainty has been lost.</summary>
	None = 0,

	/// <summary>A committed frame append may have changed the terminal sequence without a conclusive result.</summary>
	FrameSequenceAmbiguous = 1,

	/// <summary>The owning session generation changed or ended.</summary>
	SessionStateLost = 2,

	/// <summary>A correlated response established that the owning raster resource is missing.</summary>
	ResourceMissing = 3,

	/// <summary>The owning raster resource was intentionally released.</summary>
	ResourceReleased = 4,

	/// <summary>The public raster-resource wrapper was explicitly disposed.</summary>
	ExplicitResourceDisposal = 5
}
