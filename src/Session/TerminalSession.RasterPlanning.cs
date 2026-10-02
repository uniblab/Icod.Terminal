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

/// <summary>Projects bounded local persistent-raster planning observations.</summary>
public sealed partial class TerminalSession {
	/// <summary>Gets one side-effect-free advisory raster-planning snapshot.</summary>
	/// <remarks>
	/// This method performs no terminal I/O and reserves no capacity. Resource and placement counts
	/// are captured together; the animation-frame count is captured under its separate registry, so
	/// concurrent work can make the combined observation stale immediately. Allocation results remain
	/// authoritative.
	/// </remarks>
	public TerminalRasterPlanningSnapshot GetRasterPlanningSnapshot() {
		(int resourceCount, int placementCount) =
			this.persistentRasterRegistry.CapturePlanningCounts();
		return new TerminalRasterPlanningSnapshot(
			resourceCount,
			placementCount,
			this.persistentRasterAnimationRegistry.AllocatedFrameCount
		);
	}
}
