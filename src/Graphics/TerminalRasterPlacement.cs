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
/// Represents one opaque placement of a terminal-resident raster resource.
/// </summary>
public sealed class TerminalRasterPlacement : IAsyncDisposable {
	private TerminalSession? session;

	internal TerminalRasterPlacement(
		TerminalSession session,
		TerminalPersistentRasterPlacementState state
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( state );

		this.session = session;
		this.State = state;
	}

	internal TerminalPersistentRasterPlacementState State {
		get;
	}

	/// <summary>
	/// Releases this placement's local ownership. Terminal-side deletion is added by the
	/// deterministic disposal tranche.
	/// </summary>
	public ValueTask DisposeAsync() {
		TerminalSession? owner = Interlocked.Exchange(
			ref this.session,
			null
		);
		return owner is null
			? ValueTask.CompletedTask
			: owner.ReleasePersistentRasterPlacementAsync( this.State )
		;
	}
}
