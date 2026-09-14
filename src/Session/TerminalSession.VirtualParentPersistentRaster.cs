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
/// Integrates physical persistent-raster descendants beneath virtual raster placeholders.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalPersistentRasterVirtualParentRegistry persistentRasterVirtualParents = new();

	internal string? GetRelativePersistentRasterPlacementCreationUnavailableMessage(
		TerminalPersistentRasterResourceState resourceState,
		TerminalPersistentRasterPlaceholderState parentState
	) {
		ArgumentNullException.ThrowIfNull( resourceState );
		ArgumentNullException.ThrowIfNull( parentState );

		if ( !this.persistentRasterRegistry.IsResourceCurrent( resourceState ) ) {
			return "The persistent raster resource is no longer current for this session generation.";
		}
		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( parentState ) ) {
			return "The parent raster placeholder is no longer current for this session generation.";
		}

		return null;
	}

	internal int GetEffectivePersistentRasterRelativeDepth(
		TerminalPersistentRasterPlacementState placementState
	) {
		ArgumentNullException.ThrowIfNull( placementState );
		TerminalPersistentRasterPlacementState current = placementState;
		HashSet<TerminalPersistentRasterPlacementState> visited = [];
		while ( current.Parent is not null ) {
			if ( !visited.Add( current ) ) {
				throw new InvalidOperationException(
					"The persistent raster placement parent graph contains a cycle."
				);
			}
			current = current.Parent;
		}

		return current.VirtualParent is null
			? placementState.RelativeDepth
			: checked( placementState.RelativeDepth + 1 )
		;
	}

	internal async ValueTask<TerminalControlResult<TerminalRasterPlacement>> CreateRelativePersistentRasterPlacementFromPlaceholderAsync(
		TerminalPersistentRasterResourceState resourceState,
		TerminalPersistentRasterPlaceholderState parentState,
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

		if ( !this.persistentRasterRegistry.TryReservePlacement(
			resourceState,
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
				"The persistent raster registry reported a successful virtual-parent child reservation without state."
			);
		}

		try {
			placementState.BindVirtualParent(
				parentState,
				columnOffset,
				rowOffset
			);
			if ( !this.persistentRasterVirtualParents.TryRegister(
				parentState,
				placementState
			) ) {
				throw new InvalidOperationException(
					"The persistent raster virtual-parent registry rejected a freshly reserved child."
				);
			}
		} catch {
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
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
					this.persistentRasterVirtualParents.RemovePlacement( placementState );
					_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
				}
			);
		} catch {
			this.persistentRasterVirtualParents.RemovePlacement( placementState );
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		TerminalQueryResponseResult queryResult;
		try {
			queryResult = await transaction.ConfigureAwait( false );
		} catch {
			this.persistentRasterVirtualParents.RemovePlacement( placementState );
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
			this.persistentRasterVirtualParents.RemovePlacement( placementState );
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			throw;
		}

		if ( !response.IsSuccess ) {
			if ( response.IsMissingResource ) {
				this.persistentRasterVirtualParents.RemovePlacement( placementState );
				_ = this.persistentRasterRegistry.InvalidateResource( resourceState );
				return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					response.Message
				);
			}
			if ( response.IsMissingParent ) {
				this.InvalidateVirtualPlaceholderSubtree( parentState );
				return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
					response.Message
				);
			}

			this.persistentRasterVirtualParents.RemovePlacement( placementState );
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			return TerminalControlResult<TerminalRasterPlacement>.Failed(
				response.Message
			);
		}

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState )
			|| !this.persistentRasterRegistry.IsPlaceholderCurrent( parentState )
			|| !this.persistentRasterVirtualParents.Contains(
				parentState,
				placementState
			) ) {
			this.persistentRasterVirtualParents.RemovePlacement( placementState );
			_ = this.persistentRasterRegistry.TryReleasePlacement( placementState );
			return TerminalControlResult<TerminalRasterPlacement>.Unavailable(
				"The virtual-parent persistent raster placement lost generation ownership before creation completed."
			);
		}

		return TerminalControlResult<TerminalRasterPlacement>.Available(
			new TerminalRasterPlacement(
				this,
				placementState
			)
		);
	}

	internal async ValueTask<TerminalControlMutationResult> UpdateVirtualParentPersistentRasterPlacementAsync(
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

		TerminalPersistentRasterPlaceholderState? parentState = placementState.VirtualParent;
		if ( parentState is null ) {
			throw new InvalidOperationException(
				"Only a virtual-parent persistent raster placement can use the virtual-parent update path."
			);
		}
		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The persistent raster placement is no longer current for this session generation."
			);
		}
		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( parentState )
			|| !this.persistentRasterVirtualParents.Contains(
				parentState,
				placementState
			) ) {
			return TerminalControlMutationResult.Unavailable(
				"The parent raster placeholder is no longer current for this session generation."
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
				"The parent raster placeholder no longer has a usable terminal image identity."
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
			if ( response.IsMissingResource ) {
				this.persistentRasterVirtualParents.RemovePlacement( placementState );
				_ = this.persistentRasterRegistry.InvalidateResource(
					placementState.Resource
				);
				return TerminalControlMutationResult.Unavailable(
					response.Message
				);
			}
			if ( response.IsMissingParent ) {
				this.InvalidateVirtualPlaceholderSubtree( parentState );
				return TerminalControlMutationResult.Unavailable(
					response.Message
				);
			}

			return TerminalControlMutationResult.Failed(
				response.Message
			);
		}

		if ( !this.persistentRasterRegistry.IsPlacementCurrent( placementState )
			|| !this.persistentRasterRegistry.IsPlaceholderCurrent( parentState )
			|| !this.persistentRasterVirtualParents.Contains(
				parentState,
				placementState
			) ) {
			return TerminalControlMutationResult.Unavailable(
				"The virtual-parent persistent raster placement is no longer current for this session generation."
			);
		}

		placementState.CommitRelativeOffsets(
			columnOffset,
			rowOffset
		);
		return TerminalControlMutationResult.Success();
	}

	internal async ValueTask ReleasePersistentRasterPlaceholderWithDescendantsAsync(
		TerminalPersistentRasterPlaceholderState placeholderState
	) {
		ArgumentNullException.ThrowIfNull( placeholderState );
		if ( !this.persistentRasterRegistry.IsPlaceholderCurrent( placeholderState ) ) {
			_ = this.ReleaseVirtualPlaceholderChildren( placeholderState );
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			return;
		}

		IDisposable outputLease;
		try {
			outputLease = await this.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} catch {
			_ = this.ReleaseVirtualPlaceholderChildren( placeholderState );
			_ = this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState );
			throw;
		}

		using ( outputLease ) {
			TerminalPersistentRasterPlacementState[] releasedPlacements =
				this.ReleaseVirtualPlaceholderChildren( placeholderState );
			if ( !this.persistentRasterRegistry.TryReleasePlaceholder( placeholderState ) ) {
				return;
			}

			foreach ( TerminalPersistentRasterPlacementState placement in releasedPlacements ) {
				uint placementImageId = placement.Resource.ImageId;
				if ( 0u == placementImageId ) {
					continue;
				}

				await this.WritePersistentRasterControlFrameCoreAsync(
					KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
						placementImageId,
						placement.PlacementId
					)
				).ConfigureAwait( false );
			}

			uint imageId = placeholderState.Resource.ImageId;
			if ( 0u != imageId ) {
				await this.WritePersistentRasterControlFrameCoreAsync(
					KittyGraphicsPersistentEncoder.EncodeDeletePlacementPayload(
						imageId,
						placeholderState.PlacementId
					)
				).ConfigureAwait( false );
			}
			await this.Output.FlushAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		}
	}

	private TerminalPersistentRasterPlacementState[] ReleaseVirtualPlaceholderChildren(
		TerminalPersistentRasterPlaceholderState parentState
	) {
		ArgumentNullException.ThrowIfNull( parentState );
		TerminalPersistentRasterPlacementState[] roots =
			this.persistentRasterVirtualParents.TakeChildren( parentState );
		List<TerminalPersistentRasterPlacementState> released = [];
		foreach ( TerminalPersistentRasterPlacementState root in roots ) {
			if ( this.persistentRasterRegistry.TryReleasePlacement(
				root,
				out TerminalPersistentRasterPlacementState[] subtree
			) ) {
				released.AddRange( subtree );
			}
		}

		TerminalPersistentRasterPlacementState[] result = released.ToArray();
		Array.Sort(
			result,
			static ( left, right ) => {
				int depthOrder = right.RelativeDepth.CompareTo( left.RelativeDepth );
				if ( 0 != depthOrder ) {
					return depthOrder;
				}
				int resourceOrder = left.Resource.ImageNumber.CompareTo(
					right.Resource.ImageNumber
				);
				return 0 != resourceOrder
					? resourceOrder
					: left.PlacementId.CompareTo( right.PlacementId )
				;
			}
		);
		return result;
	}

	private void InvalidateVirtualPlaceholderSubtree(
		TerminalPersistentRasterPlaceholderState parentState
	) {
		ArgumentNullException.ThrowIfNull( parentState );
		_ = parentState.TryMarkStale(
			TerminalRasterOwnershipLossReason.ParentPlacementLost
		);
		TerminalPersistentRasterPlacementState[] roots =
			this.persistentRasterVirtualParents.TakeChildren( parentState );
		foreach ( TerminalPersistentRasterPlacementState root in roots ) {
			_ = this.persistentRasterRegistry.InvalidatePlacementSubtree( root );
		}
		_ = this.persistentRasterRegistry.TryReleasePlaceholder( parentState );
	}
}
