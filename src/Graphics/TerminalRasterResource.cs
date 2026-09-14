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
	/// Gets one side-effect-free snapshot of Icod.Terminal's current local ownership certainty for this resource.
	/// </summary>
	/// <remarks>
	/// A current result is local certainty only; it is not authenticated proof that terminal-side storage still exists.
	/// </remarks>
	public TerminalRasterOwnershipState OwnershipState {
		get {
			return Volatile.Read( ref this.session ) is null
				? new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Disposed,
					TerminalRasterOwnershipLossReason.ExplicitDisposal
				)
				: new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Current,
					TerminalRasterOwnershipLossReason.None
				)
			;
		}
	}

	/// <summary>
	/// Creates one opaque placement of this resource at the terminal's current cursor position.
	/// </summary>
	/// <param name="options">Optional persistent-raster placement geometry.</param>
	/// <param name="cancellationToken">Cancellation observed before placement output commits.</param>
	/// <returns>
	/// An available opaque placement, or a controlled unavailable result when the session cannot
	/// reserve another persistent placement.
	/// </returns>
	public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreatePlacementAsync(
		TerminalRasterPlacementOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		options?.Validate(
			this.State.SourceWidth,
			this.State.SourceHeight
		);
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
	/// Creates one opaque placement of this resource at a signed terminal-cell offset from an
	/// existing placement. The selected parent is immutable for the child's complete lifetime.
	/// </summary>
	/// <param name="parent">
	/// The current placement that establishes relative positioning and descendant lifetime.
	/// </param>
	/// <param name="columnOffset">The signed horizontal offset from the parent in terminal cells.</param>
	/// <param name="rowOffset">The signed vertical offset from the parent in terminal cells.</param>
	/// <param name="options">Optional persistent-raster placement geometry.</param>
	/// <param name="cancellationToken">Cancellation observed before placement output commits.</param>
	/// <returns>
	/// An available opaque child placement, or a controlled unavailable result when the relative
	/// placement cannot be established.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="parent"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="parent"/> belongs to a different terminal session.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// This resource or <paramref name="parent"/> has already been disposed.
	/// </exception>
	public ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreateRelativePlacementAsync(
		TerminalRasterPlacement parent,
		int columnOffset,
		int rowOffset,
		TerminalRasterPlacementOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( parent );
		options?.Validate(
			this.State.SourceWidth,
			this.State.SourceHeight
		);
		cancellationToken.ThrowIfCancellationRequested();

		TerminalSession? owner = Volatile.Read( ref this.session );
		if ( owner is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterResource ),
				"The persistent raster resource has already been disposed."
			);
		}

		TerminalSession? parentOwner = parent.Owner;
		if ( parentOwner is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterPlacement ),
				"The parent persistent raster placement has already been disposed."
			);
		}
		if ( !ReferenceEquals(
			owner,
			parentOwner
		) ) {
			throw new ArgumentException(
				"The parent persistent raster placement must belong to the same terminal session as this resource.",
				nameof( parent )
			);
		}

		string? unavailableMessage = owner.GetRelativePersistentRasterPlacementCreationUnavailableMessage(
			this.State,
			parent.State
		);
		if ( unavailableMessage is not null ) {
			return ValueTask.FromResult(
				TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					unavailableMessage
				)
			);
		}

		return owner.CreateRelativePersistentRasterPlacementAsync(
			this.State,
			parent.State,
			columnOffset,
			rowOffset,
			options,
			cancellationToken
		);
	}

	/// <summary>
	/// Releases this resource's local ownership, deletes its current placements, and then attempts
	/// one terminal-side resource-data deletion while its terminal identity remains current.
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
