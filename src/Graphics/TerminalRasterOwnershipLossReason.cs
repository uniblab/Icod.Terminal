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
/// Describes the backend-neutral reason associated with a non-current persistent raster ownership state.
/// </summary>
public enum TerminalRasterOwnershipLossReason {
	/// <summary>
	/// No ownership loss or release reason applies.
	/// </summary>
	None = 0,

	/// <summary>
	/// Session lifecycle or explicit invalidation removed terminal-state certainty.
	/// </summary>
	SessionStateLost = 1,

	/// <summary>
	/// Correlated terminal evidence established that the affected raster resource identity is missing.
	/// </summary>
	ResourceMissing = 2,

	/// <summary>
	/// Correlated terminal evidence established that the required parent-placement relationship is missing.
	/// </summary>
	ParentPlacementLost = 3,

	/// <summary>
	/// Placement lifetime ended because an ancestor placement was intentionally released.
	/// </summary>
	AncestorReleased = 4,

	/// <summary>
	/// Placement lifetime ended because its owning raster resource was intentionally released.
	/// </summary>
	ResourceReleased = 5,

	/// <summary>
	/// The public wrapper itself was explicitly disposed by its caller.
	/// </summary>
	ExplicitDisposal = 6
}
