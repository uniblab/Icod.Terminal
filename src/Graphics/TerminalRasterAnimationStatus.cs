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
/// Describes Icod.Terminal's current local certainty for one persistent-raster animation sequence.
/// </summary>
public enum TerminalRasterAnimationStatus {
	/// <summary>The known animation frame sequence is current.</summary>
	Current = 0,

	/// <summary>The resource remains owned, but the exact terminal-side frame tail is uncertain.</summary>
	SequenceUncertain = 1,

	/// <summary>The animation is stale because its resource or session certainty was lost.</summary>
	Stale = 2,

	/// <summary>The owning resource was intentionally released.</summary>
	Released = 3,

	/// <summary>The owning resource wrapper was explicitly disposed.</summary>
	OwnerDisposed = 4
}
