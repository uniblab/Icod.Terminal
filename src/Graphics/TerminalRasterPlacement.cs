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
	/// Replaces this placement at the terminal's current cursor position while retaining its
	/// private resource and placement identities.
	/// </summary>
	/// <param name="options">Optional persistent-raster placement geometry.</param>
	/// <param name="cancellationToken">Cancellation observed before replacement output commits.</param>
	/// <returns>The controlled mutation result.</returns>
	public ValueTask<TerminalControlMutationResult> UpdateAsync(
		TerminalRasterPlacementOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		options?.Validate(
			this.State.Resource.SourceWidth,
			this.State.Resource.SourceHeight
		);
		cancellationToken.ThrowIfCancellationRequested();

		TerminalSession? owner = Volatile.Read( ref this.session );
		if ( owner is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterPlacement ),
				"The persistent raster placement has already been disposed."
			);
		}

		return owner.UpdatePersistentRasterPlacementAsync(
			this.State,
			options,
			cancellationToken
		);
	}

	/// <summary>
	/// Releases this placement's local ownership and, while its terminal identity remains current,
	/// attempts one targeted terminal-side placement deletion.
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
