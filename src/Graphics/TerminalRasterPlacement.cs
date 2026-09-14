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

	internal TerminalSession? Owner {
		get {
			return Volatile.Read( ref this.session );
		}
	}

	internal TerminalPersistentRasterPlacementState State {
		get;
	}

	/// <summary>
	/// Gets one side-effect-free snapshot of Icod.Terminal's current local ownership certainty for this placement.
	/// </summary>
	/// <remarks>
	/// A current result is local certainty only; it is not authenticated proof that terminal-side placement still exists.
	/// </remarks>
	public TerminalRasterOwnershipState OwnershipState {
		get {
			return Volatile.Read( ref this.session ) is null
				? new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Disposed,
					TerminalRasterOwnershipLossReason.ExplicitDisposal
				)
				: this.State.ObserveOwnershipState()
			;
		}
	}

	/// <summary>
	/// Replaces this placement while retaining its established positioning mode and private
	/// resource and placement identities. A relative placement retains its immutable parent and
	/// current relative cell offsets while common placement geometry is replaced.
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

		return this.State.Parent is null
			? owner.UpdatePersistentRasterPlacementAsync(
				this.State,
				options,
				cancellationToken
			)
			: owner.UpdateRelativePersistentRasterPlacementAsync(
				this.State,
				this.State.ColumnOffset,
				this.State.RowOffset,
				options,
				cancellationToken
			)
		;
	}

	/// <summary>
	/// Replaces the signed terminal-cell offsets and common placement geometry of a relative
	/// placement while retaining its immutable parent and private resource/placement identities.
	/// </summary>
	/// <param name="columnOffset">The signed horizontal offset from the parent in terminal cells.</param>
	/// <param name="rowOffset">The signed vertical offset from the parent in terminal cells.</param>
	/// <param name="options">Optional persistent-raster placement geometry.</param>
	/// <param name="cancellationToken">Cancellation observed before replacement output commits.</param>
	/// <returns>The controlled mutation result.</returns>
	/// <exception cref="InvalidOperationException">
	/// This placement was created as an ordinary current-cursor placement rather than a relative placement.
	/// </exception>
	/// <exception cref="ObjectDisposedException">This placement has already been disposed.</exception>
	public ValueTask<TerminalControlMutationResult> UpdateRelativeAsync(
		int columnOffset,
		int rowOffset,
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
		if ( this.State.Parent is null ) {
			throw new InvalidOperationException(
				"Only a relative persistent raster placement can update relative offsets."
			);
		}

		return owner.UpdateRelativePersistentRasterPlacementAsync(
			this.State,
			columnOffset,
			rowOffset,
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
