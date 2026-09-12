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
/// Represents one opaque terminal-resident raster resource owned by a terminal session.
/// </summary>
public sealed class TerminalRasterResource : IAsyncDisposable {
	private TerminalSession? session;

	internal TerminalRasterResource(
		TerminalSession session,
		TerminalPersistentRasterResourceState state
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( state );

		this.session = session;
		this.State = state;
	}

	internal TerminalPersistentRasterResourceState State {
		get;
	}

	/// <summary>
	/// Creates one opaque placement of this resource at the terminal's current cursor position.
	/// </summary>
	/// <param name="options">Optional terminal-cell placement extents.</param>
	/// <param name="cancellationToken">Cancellation observed before placement output commits.</param>
	/// <returns>
	/// An available opaque placement, or a controlled unavailable result when the session cannot
	/// reserve another persistent placement.
	/// </returns>
	public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreatePlacementAsync(
		TerminalRasterPlacementOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		options?.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		TerminalSession? owner = Volatile.Read( ref this.session );
		if ( owner is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterResource ),
				"The persistent raster resource has already been disposed."
			);
		}

		return owner.CreatePersistentRasterPlacementAsync(
			this.State,
			options,
			cancellationToken
		);
	}

	/// <summary>
	/// Releases this resource's local ownership. Terminal-side deletion is added by the
	/// deterministic disposal tranche after placement ownership is available.
	/// </summary>
	public ValueTask DisposeAsync() {
		TerminalSession? owner = Interlocked.Exchange(
			ref this.session,
			null
		);
		return owner is null
			? ValueTask.CompletedTask
			: owner.ReleasePersistentRasterResourceAsync( this.State )
		;
	}
}
