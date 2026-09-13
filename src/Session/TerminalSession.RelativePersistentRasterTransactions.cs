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
/// Executes acknowledged relative persistent-raster placement transactions.
/// </summary>
public sealed partial class TerminalSession {
	internal async ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreateRelativePersistentRasterPlacementAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalPersistentRasterPlacementState parentState,
		int columnOffset,
		int rowOffset,
		TerminalRasterPlacementOptions? options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( parentState );
		options?.Validate(
			resourceState.SourceWidth,
			resourceState.SourceHeight
		);
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();
		if ( resourceState.IsClosed ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterResource ),
				"The persistent raster resource has already been disposed."
			);
		}

		string? unavailableMessage = this.GetRelativePersistentRasterPlacementCreationUnavailableMessage(
			resourceState,
			parentState
		);
		if ( unavailableMessage is not null ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				unavailableMessage
			);
		}

		uint imageId = resourceState.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"Relative persistent raster placement transport requires a terminal-assigned child image identity."
			);
		}
		uint parentImageId = parentState.Resource.ImageId;
		if ( 0u == parentImageId ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"Relative persistent raster placement transport requires a terminal-assigned parent image identity."
			);
		}

		if ( !this.persistentRasterRegistry.TryReserveRelativePlacement(
			resourceState,
			parentState,
			columnOffset,
			rowOffset,
			out TerminalPersistentRasterPlacementState? placementState
		) ) {
			unavailableMessage = this.GetRelativePersistentRasterPlacementCreationUnavailableMessage(
				resourceState,
				parentState
			);
			if ( unavailableMessage is not null ) {
				return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					unavailableMessage
				);
			}
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				$"The session already owns the maximum {TerminalPersistentRasterRegistry.MaximumPlacements} persistent raster placements."
			);
		}
		if ( placementState is null ) {
			throw new InvalidOperationException(
				"The persistent raster registry reported a successful relative placement reservation without state."
			);
		}

		KittyGraphicsPersistentPlacementResponseMatcher matcher = new(
			imageId,
			placementState.PlacementId
		);
		ValueTask<TerminalQueryResponseResult> transaction;
		try {
			transaction = this.GetQueryTransactionManager().ExecuteAsync(
				_ => KittyGraphicsPersistentPlacementTransaction.WriteRelativeCoreAsync(
					this,
					imageId,
					placementState.PlacementId,
					parentImageId,
					parentState.PlacementId,
					columnOffset,
					rowOffset,
					options
				),
				TerminalQueryResponsePlan.ForCompletion( matcher ),
				PersistentRasterCreationTimeout,
				TerminalQueryTransactionManager.DefaultLateResponseOwnership,
				cancellationToken,
				abandonedCleanup: () => {
					_ = this.persistentRasterRegistry.TryReleasePlacement(
						placementState
					);
				}
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await transaction.ConfigureAwait( false );
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		KittyGraphicsPersistentPlacementResponse response;
		try {
			response = KittyGraphicsPersistentPlacementResponse.Parse(
				queryResult.Frame,
				imageId,
				placementState.PlacementId
			);
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		if ( !response.IsSuccess ) {
			if ( response.IsUnavailable ) {
				_ = this.persistentRasterRegistry.InvalidateResource( resourceState );
				return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					response.Message
				);
			}

			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			return TerminalControlResult<TerminalRasterPlacement>.Failed(
				response.Message
			);
		}

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"The relative persistent raster placement lost generation ownership before creation completed."
			);
		}

		return TerminalControlResult<TerminalRasterPlacement>.Available(
			new TerminalRasterPlacement(
				this,
				placementState
			)
		);
	}

	internal async ValueTask<TerminalControlMutationResult> UpdateRelativePersistentRasterPlacementAsync(
		TerminalPersistentRasterPlacementState placementState,
		int columnOffset,
		int rowOffset,
		TerminalRasterPlacementOptions? options,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( placementState );
		options?.Validate(
			placementState.Resource.SourceWidth,
			placementState.Resource.SourceHeight
		);
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		TerminalPersistentRasterPlacementState? parentState = placementState.Parent;
		if ( parentState is null ) {
			throw new InvalidOperationException(
				"Only a relative persistent raster placement can update relative offsets."
			);
		}
		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster placement is no longer current for this session generation."
			);
		}
		if ( !this.persistentRasterRegistry.IsPlacementCurrent( parentState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The parent persistent raster placement is no longer current for this session generation."
			);
		}

		uint imageId = placementState.Resource.ImageId;
		if ( 0u == imageId ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster resource no longer has a usable terminal image identity."
			);
		}
		uint parentImageId = parentState.Resource.ImageId;
		if ( 0u == parentImageId ) {
			return TerminalControlMutationResult.Unavailable(
				"The parent persistent raster placement no longer has a usable terminal image identity."
			);
		}

		KittyGraphicsPersistentPlacementResponseMatcher matcher = new(
			imageId,
			placementState.PlacementId
		);
		TerminalQueryResponseResult queryResult = await this.GetQueryTransactionManager().ExecuteAsync(
			_ => KittyGraphicsPersistentPlacementTransaction.WriteRelativeCoreAsync(
				this,
				imageId,
				placementState.PlacementId,
				parentImageId,
				parentState.PlacementId,
				columnOffset,
				rowOffset,
				options
			),
			TerminalQueryResponsePlan.ForCompletion( matcher ),
			PersistentRasterCreationTimeout,
			TerminalQueryTransactionManager.DefaultLateResponseOwnership,
			cancellationToken
		).ConfigureAwait( false );

		KittyGraphicsPersistentPlacementResponse response =
			KittyGraphicsPersistentPlacementResponse.Parse(
				queryResult.Frame,
				imageId,
				placementState.PlacementId
			);
		if ( !response.IsSuccess ) {
			if ( response.IsUnavailable ) {
				_ = this.persistentRasterRegistry.InvalidateResource(
					placementState.Resource
				);
				return TerminalControlMutationResult.Unavailable(
					response.Message
				);
			}

			return TerminalControlMutationResult.Failed(
				response.Message
			);
		}

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster placement is no longer current for this session generation."
			);
		}

		placementState.CommitRelativeOffsets(
			columnOffset,
			rowOffset
		);
		return TerminalControlMutationResult.Success();
	}
}
