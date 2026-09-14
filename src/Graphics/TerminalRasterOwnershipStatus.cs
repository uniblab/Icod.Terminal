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
/// Describes Icod.Terminal's current local ownership certainty for one persistent raster handle.
/// </summary>
public enum TerminalRasterOwnershipStatus {
	/// <summary>
	/// Local ownership is current and no evidence has invalidated the identity required by the handle.
	/// This is local certainty, not authenticated proof of terminal-side existence.
	/// </summary>
	Current = 0,

	/// <summary>
	/// Terminal-resident certainty has been lost and cannot be resurrected for this handle.
	/// </summary>
	Stale = 1,

	/// <summary>
	/// Local placement ownership ended because another owner released its lifetime relationship.
	/// </summary>
	Released = 2,

	/// <summary>
	/// The public wrapper itself has been disposed by its caller.
	/// </summary>
	Disposed = 3
}
