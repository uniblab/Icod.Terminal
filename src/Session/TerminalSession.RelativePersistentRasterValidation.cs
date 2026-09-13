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
/// Supplies pre-output relative persistent-raster ownership validation.
/// </summary>
public sealed partial class TerminalSession {
	internal string? GetRelativePersistentRasterPlacementCreationUnavailableMessage(
		TerminalPersistentRasterResourceState resourceState,
		TerminalPersistentRasterPlacementState parentState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( parentState );

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return "The persistent raster resource is no longer current for this session generation.";
		}
		if ( !this.persistentRasterRegistry.IsPlacementCurrent( parentState ) ) {
			return "The parent persistent raster placement is no longer current for this session generation.";
		}
		if ( TerminalPersistentRasterRegistry.MaximumRelativeDepth <= parentState.RelativeDepth ) {
			return $"A relative persistent raster placement cannot exceed the portable maximum relative depth {TerminalPersistentRasterRegistry.MaximumRelativeDepth}.";
		}

		return null;
	}
}
