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
/// Provides persistent virtual-placement ownership and typed current-cursor output for semantic
/// raster-placeholder cells.
/// </summary>
public sealed partial class TerminalSession {
	internal async ValueTask<TerminalControlResult<TerminalRasterPlaceholder>> CreatePersistentRasterPlaceholderAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalRasterPlaceholderOptions options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		if ( resourceState.IsClosed ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterResource ),
				"The persistent raster resource has already been disposed."
			);
		}
		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
				"The persistent raster resource is no longer current for this session generation."
			);
		}

		TerminalCapabilityStatus capability = this.InspectCapability(
			TerminalCapability.UnicodeRasterPlaceholders
		);
		if ( TerminalCapabilityEndpointAvailability.Unavailable
			== capability.EndpointAvailability ) {
			return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
				"Unicode raster placeholders require an interactive terminal output endpoint."
			);
		}
		if ( TerminalCapabilitySupport.Unsupported == capability.Support ) {
			return TerminalControlResult<TerminalRasterPlaceholder>.Unsupported(
				"The verified terminal backend does not support Unicode raster placeholders."
			);
		}

		uint imageId = resourceState.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
				"The persistent raster resource no longer has a usable terminal image identity."
			);
		}

		if ( !this.persistentRasterRegistry.TryReservePlaceholder(
			resourceState,
			options.Columns,
			options.Rows,
			out TerminalPersistentRasterPlaceholderState? placeholderState
		) ) {
			if ( resourceState.IsClosed ) {
				throw new ObjectDisposedException(
					nameof( TerminalRasterResource ),
					"The persistent raster resource has already been disposed."
				);
			}
			if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
				return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
					"The persistent raster resource is no longer current for this session generation."
				);
			}
			return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
				$"The session already owns the maximum {TerminalPersistentRasterRegistry.MaximumPlacements} persistent raster placements."
			);
		}
		if ( placeholderState is null ) {
			throw new InvalidOperationException(
				"The persistent raster registry reported a successful placeholder reservation without state."
			);
		}

		KittyGraphicsPersistentPlacementResponseMatcher matcher = new(
			imageId,
			placeholderState.PlacementId
		);
		ValueTask<TerminalQueryResponseResult> transaction;
		try {
			transaction = this.GetQueryTransactionManager().ExecuteAsync(
				_ => KittyGraphicsPersistentPlaceholderTransaction.WriteCoreAsync(
					this,
					imageId,
					placeholderState.PlacementId,
					placeholderState.Columns,
					placeholderState.Rows
				),
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				PersistentRasterCreationTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken,
				abandonedCleanup: () => {
					_ = this.persistentRasterRegistry.TryReleasePlaceholder(
						placeholderState
					);
				}
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			throw;
		}

		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await transaction.ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			throw;
		}

		KittyGraphicsPersistentPlacementResponse response;
		try {
			response = KittyGraphicsPersistentPlacementResponse.Parse(
				queryResult.Frame,
				imageId,
				placeholderState.PlacementId
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			throw;
		}

		if ( !response.IsSuccess ) {
			if ( response.IsMissingResource ) {
				_ = this.InvalidatePersistentRasterResourceWithVirtualDescendants(
					resourceState
				);
				return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
					response.Message
				);
			}

			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			return response.IsUnavailable
				? TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
					response.Message
				)
				: TerminalControlResult<TerminalRasterPlaceholder>.Failed(
					response.Message
				)
			;
		}

		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( placeholderState ) ) {
			return TerminalControlResult<TerminalRasterPlaceholder>.Unavailable(
				"The raster placeholder lost generation ownership before creation completed."
			);
		}

		this.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyUnicodeRasterPlaceholders,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return TerminalControlResult<TerminalRasterPlaceholder>.Available(
			new TerminalRasterPlaceholder(
				this,
				placeholderState
			)
		);
	}

	internal async ValueTask ReleasePersistentRasterPlaceholderAsync(
		TerminalPersistentRasterPlaceholderState placeholderState
	) {
		ArgumentNullException.ThrowIfNull( placeholderState );
		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( placeholderState ) ) {
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			return;
		}

		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			throw;
		}

		using ( outputLease ) {
			if ( !this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState ) ) {
				return;
			}

			uint imageId = placeholderState.Resource.ImageId;
			if ( 0u == imageId ) {
				return;
			}

			await this.WritePersistentRasterControlFrameCoreAsync(
				KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
					imageId,
					placeholderState.PlacementId
				)
			).ConfigureAwait( false );
			await this.Output.FlushAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	/// <summary>
	/// Writes one semantic raster-placeholder cell at the terminal's current text cursor position.
	/// </summary>
	/// <param name="cell">The opaque semantic placeholder cell.</param>
	/// <param name="cancellationToken">Cancellation observed before placeholder output commits.</param>
	/// <returns>A value task representing the write operation.</returns>
	public async ValueTask WriteRasterPlaceholderCellAsync(
		TerminalRasterPlaceholderCell cell,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		TerminalPersistentRasterPlaceholderState placeholderState =
			this.ValidateRasterPlaceholderCellForOutput(
				cell,
				nameof( cell )
			);

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();
		placeholderState = this.ValidateRasterPlaceholderCellForOutput(
			cell,
			nameof( cell )
		);

		await this.Output.WriteAsync(
			KittyGraphicsPlaceholderCellEncoder.Encode(
				placeholderState,
				cell.Row,
				cell.Column
			),
			CancellationToken.None
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Writes semantic raster-placeholder cells in caller-supplied order beginning at the terminal's
	/// current text cursor position.
	/// </summary>
	/// <param name="cells">The ordered semantic placeholder cells to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before placeholder output commits.</param>
	/// <returns>A value task representing the write operation.</returns>
	public async ValueTask WriteRasterPlaceholderCellsAsync(
		ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		this.ValidateRasterPlaceholderCellsForOutput(
			cells,
			nameof( cells )
		);

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateRasterPlaceholderCellsForOutput(
			cells,
			nameof( cells )
		);

		for ( int index = 0; index < cells.Length; ++index ) {
			TerminalRasterPlaceholderCell cell = cells.Span[ index ];
			TerminalRasterPlaceholder placeholder = cell.Owner
				?? throw new InvalidOperationException(
					"A validated raster-placeholder cell lost its owner before committed output."
				)
			;
			await this.Output.WriteAsync(
				KittyGraphicsPlaceholderCellEncoder.Encode(
					placeholder.State,
					cell.Row,
					cell.Column
				),
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	internal void ValidateRasterPlaceholderCellsForOutput(
		ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		for ( int index = 0; index < cells.Length; ++index ) {
			_ = this.ValidateRasterPlaceholderCellForOutput(
				cells.Span[ index ],
				parameterName
			);
		}
	}

	internal TerminalPersistentRasterPlaceholderState ValidateRasterPlaceholderCellForOutput(
		TerminalRasterPlaceholderCell cell,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		TerminalRasterPlaceholder? placeholder = cell.Owner;
		if ( placeholder is null ) {
			throw new ArgumentException(
				"The raster-placeholder cell does not identify an owning placeholder.",
				parameterName
			);
		}

		TerminalSession? owner = placeholder.Owner;
		if ( owner is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterPlaceholder ),
				"The raster placeholder that owns this cell has already been disposed."
			);
		}
		if ( !ReferenceEquals(
			this,
			owner
		) ) {
			throw new ArgumentException(
				"The raster-placeholder cell must belong to this terminal session.",
				parameterName
			);
		}

		TerminalPersistentRasterPlaceholderState state = placeholder.State;
		TerminalRasterOwnershipState ownership = state.ObserveOwnershipState();
		if ( TerminalRasterOwnershipStatus.Current != ownership.Status ) {
			throw new InvalidOperationException(
				$"The raster-placeholder cell cannot be emitted because its placeholder ownership status is {ownership.Status}."
			);
		}
		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( state ) ) {
			throw new InvalidOperationException(
				"The raster-placeholder cell does not belong to the current registered session generation."
			);
		}
		return state;
	}
}
